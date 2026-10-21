using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public interface IIdentityRepository
{
    Task<bool> TryBootstrapAsync(
        Organization organization,
        IReadOnlyCollection<ApplicationUser> users,
        SecurityAuditRecord auditRecord,
        CancellationToken cancellationToken);

    Task<ApplicationUser?> FindActiveUserAsync(
        string organizationCode,
        string normalizedUsername,
        CancellationToken cancellationToken);
}
