using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Core.Services;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using MS.Cobroup.BackendAdmin.Infrastructure.Security;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

const string authorityCookie = "cobroup-authority";

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("IdentityDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityDatabase is required.");
var issuer = builder.Configuration["Identity:Issuer"]
    ?? throw new InvalidOperationException("Identity:Issuer is required.");

builder.Services.AddHealthChecks();
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.HeaderName = "X-XSRF-TOKEN";
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("identity-write", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1)
        }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseNpgsql(connectionString, postgres =>
        postgres.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName));
    options.UseOpenIddict<Guid>();
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = authorityCookie;
        options.DefaultChallengeScheme = authorityCookie;
        options.DefaultSignInScheme = authorityCookie;
    })
    .AddCookie(authorityCookie, options =>
    {
        options.Cookie.Name = "__Host-cobroup-authority";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddOpenIddict()
    .AddCore(options =>
        options.UseEntityFrameworkCore()
            .UseDbContext<IdentityDbContext>()
            .ReplaceDefaultEntities<Guid>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(issuer, UriKind.Absolute));
        options.SetAuthorizationEndpointUris("connect/authorize");
        options.SetTokenEndpointUris("connect/token");
        options.SetRevocationEndpointUris("connect/revocation");
        options.SetEndSessionEndpointUris("connect/logout");
        options.SetUserInfoEndpointUris("connect/userinfo");
        options.AllowAuthorizationCodeFlow();
        options.AllowRefreshTokenFlow();
        options.RequireProofKeyForCodeExchange();
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.OfflineAccess,
            "cobroup-api");
        options.DisableAccessTokenEncryption();

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate();
            options.AddDevelopmentSigningCertificate();
        }
        else
        {
            options.AddSigningCertificate(LoadCertificate(builder.Configuration, "Signing"));
            options.AddEncryptionCertificate(LoadCertificate(builder.Configuration, "Encryption"));
        }

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddScoped<IIdentityRepository, EfIdentityRepository>();
builder.Services.AddScoped<IPasswordCredentialService, AspNetPasswordCredentialService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<object>,
    Microsoft.AspNetCore.Identity.PasswordHasher<object>>();
builder.Services.AddScoped<BootstrapIdentityService>();
builder.Services.AddScoped<AuthenticateUserService>();
builder.Services.AddSingleton<IAdministrativeScopeAuthorizer, AdministrativeScopeAuthorizer>();
builder.Services.AddHostedService<OidcClientSeeder>();

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");

app.MapGet("/api/v1/auth/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken });
});

app.MapPost("/api/v1/auth/session", async (
    LoginRequest request,
    HttpContext context,
    IAntiforgery antiforgery,
    AuthenticateUserService authentication,
    CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(context);
    var user = await authentication.ExecuteAsync(
        request.OrganizationCode,
        request.Username,
        request.Password,
        cancellationToken);

    if (user is null)
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(OpenIddictConstants.Claims.Subject, user.Id.ToString()),
        new Claim(OpenIddictConstants.Claims.Name, user.DisplayName),
        new Claim(OpenIddictConstants.Claims.PreferredUsername, user.Username),
        new Claim(OpenIddictConstants.Claims.Role, user.Role.ToString()),
        new Claim("organization_id", user.OrganizationId.ToString()),
        new Claim("organization_code", user.Organization.Code)
    };
    var identity = new ClaimsIdentity(claims, authorityCookie, OpenIddictConstants.Claims.Name, OpenIddictConstants.Claims.Role);
    await context.SignInAsync(authorityCookie, new ClaimsPrincipal(identity));

    return Results.NoContent();
}).RequireRateLimiting("identity-write");

app.MapDelete("/api/v1/auth/session", async (HttpContext context, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(context);
    await context.SignOutAsync(authorityCookie);
    return Results.NoContent();
});

app.MapPost("/api/v1/bootstrap", async (
    BootstrapRequest request,
    HttpContext context,
    BootstrapIdentityService bootstrap,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var configuredKey = configuration["Bootstrap:Key"];
    var suppliedKey = context.Request.Headers["X-Bootstrap-Key"].ToString();
    if (string.IsNullOrWhiteSpace(configuredKey))
    {
        return Results.Problem("Bootstrap is disabled until Bootstrap:Key is supplied securely.", statusCode: 503);
    }

    if (!FixedTimeEquals(configuredKey, suppliedKey))
    {
        return Results.Unauthorized();
    }

    var result = await bootstrap.ExecuteAsync(
        new BootstrapIdentityCommand(
            request.OrganizationCode,
            request.OrganizationName,
            new BootstrapUserCommand(
                request.Administrator.Username,
                request.Administrator.DisplayName,
                request.Administrator.Password),
            new BootstrapUserCommand(
                request.Supervisor.Username,
                request.Supervisor.DisplayName,
                request.Supervisor.Password),
            new BootstrapUserCommand(
                request.Collector.Username,
                request.Collector.DisplayName,
                request.Collector.Password),
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        cancellationToken);

    return result.Created
        ? Results.Created("/api/v1/auth/session", new { result.OrganizationId, result.UserIds })
        : Results.Conflict(new { message = "Platform bootstrap has already been completed." });
}).RequireRateLimiting("identity-write");

app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], async (
    HttpContext context,
    IConfiguration configuration) =>
{
    var request = context.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenID Connect request is unavailable.");
    var authentication = await context.AuthenticateAsync(authorityCookie);
    if (!authentication.Succeeded || authentication.Principal is null)
    {
        var loginUri = request.ClientId switch
        {
            "cobroup-admin-spa" => configuration["Identity:Clients:Admin:LoginUri"],
            "cobroup-operational-spa" => configuration["Identity:Clients:Operational:LoginUri"],
            _ => null
        };
        return string.IsNullOrWhiteSpace(loginUri)
            ? Results.Problem("The login URI for this client is not configured.", statusCode: 500)
            : Results.Redirect(loginUri);
    }

    var identity = new ClaimsIdentity(
        authentication.Principal.Claims,
        TokenValidationParameters.DefaultAuthenticationType,
        OpenIddictConstants.Claims.Name,
        OpenIddictConstants.Claims.Role);
    var principal = new ClaimsPrincipal(identity);
    principal.SetScopes(request.GetScopes());
    principal.SetResources("cobroup-api");

    foreach (var claim in principal.Claims)
    {
        claim.SetDestinations(claim.Type switch
        {
            OpenIddictConstants.Claims.Name or
            OpenIddictConstants.Claims.PreferredUsername or
            OpenIddictConstants.Claims.Role or
            "organization_id" or
            "organization_code" =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        });
    }

    return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

app.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], async (HttpContext context) =>
{
    await context.SignOutAsync(authorityCookie);
    return Results.SignOut(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
});

app.MapGet("/connect/userinfo", (ClaimsPrincipal user) => Results.Ok(new
{
    sub = user.FindFirstValue(OpenIddictConstants.Claims.Subject),
    name = user.FindFirstValue(OpenIddictConstants.Claims.Name),
    preferred_username = user.FindFirstValue(OpenIddictConstants.Claims.PreferredUsername),
    role = user.FindFirstValue(OpenIddictConstants.Claims.Role),
    organization_id = user.FindFirstValue("organization_id"),
    organization_code = user.FindFirstValue("organization_code")
})).RequireAuthorization(policy =>
{
    policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
    policy.RequireAuthenticatedUser();
});

app.MapGet("/api/v1/auth/me", (ClaimsPrincipal user) => Results.Ok(new
{
    userId = user.FindFirstValue(OpenIddictConstants.Claims.Subject),
    organizationId = user.FindFirstValue("organization_id"),
    organizationCode = user.FindFirstValue("organization_code"),
    username = user.FindFirstValue(OpenIddictConstants.Claims.PreferredUsername),
    displayName = user.FindFirstValue(OpenIddictConstants.Claims.Name),
    role = user.FindFirstValue(OpenIddictConstants.Claims.Role)
})).RequireAuthorization(policy =>
{
    policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
    policy.RequireAuthenticatedUser();
});

app.Run();

static bool FixedTimeEquals(string expected, string supplied)
{
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
    return expectedBytes.Length == suppliedBytes.Length
        && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
}

static X509Certificate2 LoadCertificate(IConfiguration configuration, string purpose)
{
    var path = configuration[$"Identity:Certificates:{purpose}:Path"];
    var password = configuration[$"Identity:Certificates:{purpose}:Password"];
    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            $"Identity:Certificates:{purpose}:Path and Password are required outside Development.");
    }

    return X509CertificateLoader.LoadPkcs12FromFile(path, password);
}

public sealed record LoginRequest(string OrganizationCode, string Username, string Password);

public sealed record BootstrapRequest(
    string OrganizationCode,
    string OrganizationName,
    BootstrapUserRequest Administrator,
    BootstrapUserRequest Supervisor,
    BootstrapUserRequest Collector);

public sealed record BootstrapUserRequest(string Username, string DisplayName, string Password);

public partial class Program;
