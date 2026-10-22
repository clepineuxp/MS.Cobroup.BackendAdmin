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
using MS.Cobroup.BackendAdmin.Infrastructure.Agenda;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using MS.Cobroup.BackendAdmin.Infrastructure.Customers;
using MS.Cobroup.BackendAdmin.Infrastructure.LoanExceptions;
using MS.Cobroup.BackendAdmin.Infrastructure.Loans;
using MS.Cobroup.BackendAdmin.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("IdentityDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityDatabase is required.");
var issuer = builder.Configuration["Identity:Issuer"]
    ?? throw new InvalidOperationException("Identity:Issuer is required.");

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Service is live"), tags: ["live"])
    .AddCheck<IdentityDatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
});

var rsaKeyService = new RsaKeyService(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IRsaKeyService>(rsaKeyService);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = rsaKeyService.TokenValidationParameters;
    });

builder.Services.AddScoped<ITokenService, JwtTokenService>();

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
builder.Services.AddScoped<IPaymentMethodRepository, EfPaymentMethodRepository>();
builder.Services.AddScoped<PaymentMethodService>();
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

var app = builder.Build();

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

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.MapControllers();

app.Run();

public partial class Program;
