using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Routes;

public sealed class ListRoutesService(IRouteManagementRepository repository)
{
    public async Task<ListRoutesResult> ExecuteAsync(
        TenantActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return ListRoutesResult.Forbidden();
        }

        var routes = await repository.ListAsync(actor.OrganizationId, cancellationToken);
        return ListRoutesResult.Success(routes);
    }
}

public enum ListRoutesOutcome
{
    Success,
    Forbidden
}

public sealed record ListRoutesResult(
    ListRoutesOutcome Outcome,
    IReadOnlyCollection<RouteSummary> Routes)
{
    public static ListRoutesResult Success(IReadOnlyCollection<RouteSummary> routes) =>
        new(ListRoutesOutcome.Success, routes);

    public static ListRoutesResult Forbidden() =>
        new(ListRoutesOutcome.Forbidden, []);
}
