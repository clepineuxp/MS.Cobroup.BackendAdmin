using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

public interface IUserEditingRepository
{
    Task<OrganizationMembership?> FindAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<bool> HasOtherMembershipsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<bool> HasActiveRouteAssignmentsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<bool> SaveAsync(OrganizationMembership membership, AdministrativeAuditRecord audit,
        bool revokeAllSessions, bool revokeTenantSessions, CancellationToken cancellationToken);
}
