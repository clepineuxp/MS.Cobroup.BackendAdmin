using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Routes;

public sealed class ListAccessibleRoutesService(IRouteManagementRepository repository)
{
    public async Task<IReadOnlyCollection<AccessibleRouteSummary>> ExecuteAsync(
        TenantActor actor,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(actor.Role))
        {
            return [];
        }

        return await repository.ListAccessibleAsync(
            actor.OrganizationId,
            actor.UserId,
            actor.Role,
            cancellationToken);
    }
}
