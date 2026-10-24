using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.RouteFinances;

public interface IOperationalRouteFinanceDirectory
{
    Task<RouteFinanceProxyResult> GetBalanceAsync(string accessToken, Guid routeId, CancellationToken cancellationToken);
    Task<RouteFinanceProxyResult> ListEntriesAsync(string accessToken, Guid routeId, string section, CancellationToken cancellationToken);
    Task<RouteFinanceProxyResult> CreateEntryAsync(string accessToken, string json, CancellationToken cancellationToken);
    Task<RouteFinanceProxyResult> UpdateEntryAsync(string accessToken, Guid entryId, string json, CancellationToken cancellationToken);
}

public sealed class RouteFinanceAdministrationService(IOperationalRouteFinanceDirectory directory)
{
    public Task<RouteFinanceProxyResult> GetBalanceAsync(
        TenantActor actor, string accessToken, Guid routeId, CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.GetBalanceAsync(accessToken, routeId, cancellationToken)
            : Task.FromResult(RouteFinanceProxyResult.Forbidden());

    public Task<RouteFinanceProxyResult> ListEntriesAsync(
        TenantActor actor, string accessToken, Guid routeId, string section, CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.ListEntriesAsync(accessToken, routeId, section, cancellationToken)
            : Task.FromResult(RouteFinanceProxyResult.Forbidden());

    public Task<RouteFinanceProxyResult> CreateEntryAsync(
        TenantActor actor, string accessToken, string json, CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.CreateEntryAsync(accessToken, json, cancellationToken)
            : Task.FromResult(RouteFinanceProxyResult.Forbidden());

    public Task<RouteFinanceProxyResult> UpdateEntryAsync(
        TenantActor actor, string accessToken, Guid entryId, string json, CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.UpdateEntryAsync(accessToken, entryId, json, cancellationToken)
            : Task.FromResult(RouteFinanceProxyResult.Forbidden());
}

public sealed record RouteFinanceProxyResult(int StatusCode, string? Content, string ContentType)
{
    public static RouteFinanceProxyResult Forbidden() => new(403, null, "application/json");
    public static RouteFinanceProxyResult Unavailable() =>
        new(503, "{\"message\":\"El servicio financiero operativo no está disponible.\"}", "application/json");
}
