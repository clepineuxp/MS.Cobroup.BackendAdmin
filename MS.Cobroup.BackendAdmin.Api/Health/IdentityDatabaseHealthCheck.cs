using Microsoft.Extensions.Diagnostics.HealthChecks;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

namespace MS.Cobroup.BackendAdmin.Api.Health;

public sealed class IdentityDatabaseHealthCheck(IdentityDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Identity database connection is healthy.")
                : HealthCheckResult.Unhealthy("Cannot connect to identity database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Identity database error: {ex.Message}");
        }
    }
}
