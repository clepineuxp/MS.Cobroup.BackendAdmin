using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Core.LoanExceptions;
using MS.Cobroup.BackendAdmin.Core.LoanTemplates;
using MS.Cobroup.BackendAdmin.Core.Configuration;
using MS.Cobroup.BackendAdmin.Core.Customers;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Core.Services;
using MS.Cobroup.BackendAdmin.Core.Users;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using MS.Cobroup.BackendAdmin.Infrastructure.Customers;
using MS.Cobroup.BackendAdmin.Infrastructure.LoanExceptions;
using MS.Cobroup.BackendAdmin.Infrastructure.Security;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("IdentityDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityDatabase is required.");
var issuer = builder.Configuration["Identity:Issuer"]
    ?? throw new InvalidOperationException("Identity:Issuer is required.");

builder.Services.AddHealthChecks();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("tenant-admin", policy =>
    {
        policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(UserRole.Admin.ToString());
        policy.RequireClaim("organization_id");
    });
    options.AddPolicy("tenant-user", policy =>
    {
        policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(OpenIddictConstants.Claims.Subject);
        policy.RequireClaim("organization_id");
        policy.RequireClaim(OpenIddictConstants.Claims.Role);
    });
});
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
        options.DefaultAuthenticateScheme = AuthenticationSchemes.AuthorityCookie;
        options.DefaultChallengeScheme = AuthenticationSchemes.AuthorityCookie;
        options.DefaultSignInScheme = AuthenticationSchemes.AuthorityCookie;
    })
    .AddCookie(AuthenticationSchemes.AuthorityCookie, options =>
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
builder.Services.AddScoped<IRouteManagementRepository, EfRouteManagementRepository>();
builder.Services.AddScoped<IUserDirectoryRepository, EfUserDirectoryRepository>();
builder.Services.AddScoped<IOrganizationSettingsRepository, EfOrganizationSettingsRepository>();
builder.Services.AddScoped<ILoanTemplateRepository, EfLoanTemplateRepository>();
builder.Services.AddScoped<IPasswordCredentialService, AspNetPasswordCredentialService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<object>,
    Microsoft.AspNetCore.Identity.PasswordHasher<object>>();
builder.Services.AddScoped<BootstrapIdentityService>();
builder.Services.AddScoped<AuthenticateUserService>();
builder.Services.AddScoped<OrganizationSessionService>();
builder.Services.AddScoped<CreateRouteService>();
builder.Services.AddScoped<ListRoutesService>();
builder.Services.AddScoped<ListAssignableUsersService>();
builder.Services.AddScoped<UpdateRouteService>();
builder.Services.AddScoped<ListAccessibleRoutesService>();
builder.Services.AddScoped<OrganizationSettingsService>();
builder.Services.AddScoped<CustomerAdministrationService>();
builder.Services.AddScoped<LoanTemplateService>();
builder.Services.AddScoped<ILoanExceptionAuditRepository, EfLoanExceptionAuditRepository>();
builder.Services.AddScoped<LoanExceptionAdministrationService>();
builder.Services.AddHttpClient<IOperationalCustomerDirectory, OperationalCustomerDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OperationalApi:BaseUrl"]
        ?? throw new InvalidOperationException("OperationalApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<IOperationalLoanExceptionDirectory, OperationalLoanExceptionDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OperationalApi:BaseUrl"]
        ?? throw new InvalidOperationException("OperationalApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<IAdministrativeScopeAuthorizer, AdministrativeScopeAuthorizer>();
builder.Services.AddHostedService<OidcClientSeeder>();

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();

app.Run();

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

public partial class Program;
