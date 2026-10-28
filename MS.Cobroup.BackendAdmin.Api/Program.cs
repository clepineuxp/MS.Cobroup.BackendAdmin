using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Api.Health;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Agenda;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Core.LoanExceptions;
using MS.Cobroup.BackendAdmin.Core.Loans;
using MS.Cobroup.BackendAdmin.Core.LoanTemplates;
using MS.Cobroup.BackendAdmin.Core.PaymentMethods;
using MS.Cobroup.BackendAdmin.Core.Configuration;
using MS.Cobroup.BackendAdmin.Core.Customers;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Core.Services;
using MS.Cobroup.BackendAdmin.Core.Users;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;
using MS.Cobroup.BackendAdmin.Infrastructure.Agenda;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using MS.Cobroup.BackendAdmin.Infrastructure.Customers;
using MS.Cobroup.BackendAdmin.Infrastructure.LoanExceptions;
using MS.Cobroup.BackendAdmin.Infrastructure.Loans;
using MS.Cobroup.BackendAdmin.Core.RouteFinances;
using MS.Cobroup.BackendAdmin.Infrastructure.RouteFinances;
using MS.Cobroup.BackendAdmin.Infrastructure.Security;
using MS.Cobroup.BackendAdmin.Infrastructure.MachineAuth;
using MS.Cobroup.BackendAdmin.Core.MachineAuth;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("IdentityDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityDatabase is required.");

// Private provisioning needs only the database and password hashing, not the HTTP/JWT host.
if (args.Contains("--provision-organization"))
{
    builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IOrganizationProvisioningRepository, EfOrganizationProvisioningRepository>();
    builder.Services.AddScoped<ProvisionOrganizationService>();
    builder.Services.AddScoped<IPasswordCredentialService, AspNetPasswordCredentialService>();
    builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<object>, Microsoft.AspNetCore.Identity.PasswordHasher<object>>();
    await using var provisioningHost = builder.Build();
    return await MS.Cobroup.BackendAdmin.Api.Provisioning.OrganizationProvisioningCli.RunAsync(
        provisioningHost.Services, Console.In, Console.Out, Console.Error);
}

var issuer = builder.Configuration["Identity:Issuer"]
    ?? throw new InvalidOperationException("Identity:Issuer is required.");

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Service is live"), tags: ["live"])
    .AddCheck<IdentityDatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDataProtection();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("tenant-admin", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(UserRole.Admin.ToString());
        policy.RequireClaim("organization_id");
    });
    options.AddPolicy("tenant-user", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("organization_id");
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? [
        "http://localhost:5200",
        "http://localhost:5300",
        "https://dev.cobroup.com",
        "https://dev-admin.cobroup.com",
        "https://qa.cobroup.com",
        "https://qa-admin.cobroup.com",
        "https://cobroup.com",
        "https://admin.cobroup.com"
    ];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-cobroup-antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.Path = "/";
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
});

var keyService = new SymmetricKeyService(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<ISymmetricKeyService>(keyService);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        var issuer = builder.Configuration["Identity:Issuer"] ?? "https://localhost:7119/";
        var audience = builder.Configuration["Identity:Audience"] ?? "cobroup-api";
        options.TokenValidationParameters = keyService.CreateTokenValidationParameters(issuer, audience);
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var validation = AccessJwtValidator.Validate(context.Principal, context.SecurityToken);
                if (!validation.IsValid)
                {
                    context.Fail(validation.FailureReason!);
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddScoped<ITokenService, JwtTokenService>();

builder.Services.AddScoped<IIdentityRepository, EfIdentityRepository>();
builder.Services.AddScoped<IRouteManagementRepository, EfRouteManagementRepository>();
builder.Services.AddScoped<IUserDirectoryRepository, EfUserDirectoryRepository>();
builder.Services.AddScoped<IUserManagementRepository, EfUserManagementRepository>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<IUserEditingRepository, EfUserEditingRepository>();
builder.Services.AddScoped<UpdateOrganizationUserService>();
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
builder.Services.AddScoped<IPaymentMethodRepository, EfPaymentMethodRepository>();
builder.Services.AddScoped<PaymentMethodService>();
builder.Services.AddScoped<IMachineAuthorizationRepository, EfMachineAuthorizationRepository>();
builder.Services.AddSingleton<IMachineOtpProtector, DataProtectionMachineOtpProtector>();
builder.Services.AddScoped<MachineAuthorizationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<MS.Cobroup.BackendAdmin.Api.Security.CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient<IOperationalCustomerDirectory, OperationalCustomerDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OperationalApi:BaseUrl"]
        ?? throw new InvalidOperationException("OperationalApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<MS.Cobroup.BackendAdmin.Api.Security.CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient<IOperationalLoanExceptionDirectory, OperationalLoanExceptionDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OperationalApi:BaseUrl"]
        ?? throw new InvalidOperationException("OperationalApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<MS.Cobroup.BackendAdmin.Api.Security.CorrelationIdDelegatingHandler>();

builder.Services.AddScoped<LoanAdministrationService>();
builder.Services.AddHttpClient<IOperationalLoanDirectory, OperationalLoanDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OperationalApi:BaseUrl"]
        ?? throw new InvalidOperationException("OperationalApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<MS.Cobroup.BackendAdmin.Api.Security.CorrelationIdDelegatingHandler>();

builder.Services.AddScoped<RouteFinanceAdministrationService>();
builder.Services.AddHttpClient<IOperationalRouteFinanceDirectory, OperationalRouteFinanceDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OperationalApi:BaseUrl"]
        ?? throw new InvalidOperationException("OperationalApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<MS.Cobroup.BackendAdmin.Api.Security.CorrelationIdDelegatingHandler>();

builder.Services.AddScoped<MapSupervisionService>();
builder.Services.AddScoped<AnalyticsAdministrationService>();
builder.Services.AddHttpClient<IOperationalAgendaDirectory, OperationalAgendaDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OperationalApi:BaseUrl"]
        ?? throw new InvalidOperationException("OperationalApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<MS.Cobroup.BackendAdmin.Api.Security.CorrelationIdDelegatingHandler>();

builder.Services.AddSingleton<IAdministrativeScopeAuthorizer, AdministrativeScopeAuthorizer>();
builder.Services.AddScoped<IOperationalSessionRevocationService, OperationalSessionRevocationService>();

var app = builder.Build();

if (args.Length > 0 && (args.Contains("--revoke-kid") || args.Contains("--revoke-all")))
{
    var exitCode = await Program.RunRevocationCliAsync(app.Services, args);
    return exitCode;
}

var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
var knownProxiesConfig = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()
    ?? ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.1"];
foreach (var proxy in knownProxiesConfig)
{
    if (System.Net.IPAddress.TryParse(proxy, out var ip))
    {
        forwardedOptions.KnownProxies.Add(ip);
    }
    else if (System.Net.IPNetwork.TryParse(proxy, out var network))
    {
        forwardedOptions.KnownIPNetworks.Add(network);
    }
}
app.UseForwardedHeaders(forwardedOptions);

app.Use(async (context, next) =>
{
    var rawCorrelationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var correlationId = !string.IsNullOrWhiteSpace(rawCorrelationId) && rawCorrelationId.Length <= 64
        ? rawCorrelationId.Trim()
        : Guid.NewGuid().ToString("D");

    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "0";

    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Correlation");
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        await next();
    }
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.MapControllers();

await app.RunAsync();
return 0;

public partial class Program
{
    public static async Task<int> RunRevocationCliAsync(IServiceProvider services, string[] cliArgs)
    {
        using var scope = services.CreateScope();
        var revocationService = scope.ServiceProvider.GetRequiredService<IOperationalSessionRevocationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RevocationCli");
        var correlationId = Guid.NewGuid().ToString("D");

        try
        {
            if (cliArgs.Contains("--revoke-all"))
            {
                var result = await revocationService.RevokeAllAsync(correlationId);
                logger.LogWarning("[REVOCATION_EMERGENCY] AffectedCount: {AffectedCount}, CorrelationId: {CorrelationId}", result.AffectedCount, correlationId);
                Console.WriteLine($"{{\"action\":\"revoke-all\",\"signingKeyId\":\"*\",\"affectedCount\":{result.AffectedCount},\"timestampUtc\":\"{result.TimestampUtc:O}\",\"correlationId\":\"{correlationId}\",\"status\":\"SUCCESS\"}}");
                return 0;
            }

            var kidIndex = Array.IndexOf(cliArgs, "--revoke-kid");
            string kid = string.Empty;
            if (kidIndex >= 0 && kidIndex + 1 < cliArgs.Length && !cliArgs[kidIndex + 1].StartsWith("--"))
            {
                kid = cliArgs[kidIndex + 1]?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(kid))
            {
                kid = Environment.GetEnvironmentVariable("JWT_REVOKE_KID")?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(kid))
            {
                Console.Error.WriteLine($"{{\"action\":\"revoke-kid\",\"error\":\"Argumento --revoke-kid requiere un parámetro kid (proporcionado como argumento o en variable de entorno JWT_REVOKE_KID).\",\"correlationId\":\"{correlationId}\",\"status\":\"ERROR\"}}");
                return 1;
            }

            var resultKid = await revocationService.RevokeBySigningKeyIdAsync(kid, correlationId);
            logger.LogInformation("[REVOCATION_SUCCESS] SigningKeyId: {SigningKeyId}, AffectedCount: {AffectedCount}, CorrelationId: {CorrelationId}", resultKid.SigningKeyId, resultKid.AffectedCount, correlationId);
            Console.WriteLine($"{{\"action\":\"revoke-kid\",\"signingKeyId\":\"{resultKid.SigningKeyId}\",\"affectedCount\":{resultKid.AffectedCount},\"timestampUtc\":\"{resultKid.TimestampUtc:O}\",\"correlationId\":\"{correlationId}\",\"status\":\"SUCCESS\"}}");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante la ejecución del comando de revocación. CorrelationId: {CorrelationId}", correlationId);
            Console.Error.WriteLine($"{{\"error\":\"{ex.Message}\",\"correlationId\":\"{correlationId}\",\"status\":\"ERROR\"}}");
            return 1;
        }
    }
}
