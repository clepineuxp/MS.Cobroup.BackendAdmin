using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Routes;

public interface IRouteManagementRepository
{
    Task<bool> NameExistsAsync(
        Guid organizationId,
        string normalizedName,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, UserRole>> GetActiveUserRolesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    Task<bool> TryAddAsync(
        CollectionRoute route,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken);
}
