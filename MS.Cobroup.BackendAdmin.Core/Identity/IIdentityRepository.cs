using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public interface IIdentityRepository
{
    Task<bool> TryBootstrapAsync(
        Organization organization,
        IReadOnlyCollection<ApplicationUser> users,
        IReadOnlyCollection<OrganizationMembership> memberships,
        SecurityAuditRecord auditRecord,
        CancellationToken cancellationToken);

    Task<ApplicationUser?> FindActiveUserAsync(
        string normalizedUsername,
        CancellationToken cancellationToken);

    Task<OrganizationMembership?> SelectMostRecentlyAccessedMembershipAsync(
        Guid userId,
        DateTimeOffset accessedAtUtc,
        CancellationToken cancellationToken);

    Task<OrganizationMembership?> SelectMembershipAsync(
        Guid userId,
        Guid organizationId,
        DateTimeOffset accessedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OrganizationMembership>> ListActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
