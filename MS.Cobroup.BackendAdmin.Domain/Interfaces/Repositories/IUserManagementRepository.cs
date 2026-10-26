using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

public interface IUserManagementRepository
{
    Task<bool> IsActiveAdminAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OrganizationUser>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<bool> TryCreateAsync(ApplicationUser user, OrganizationMembership membership,
        AdministrativeAuditRecord audit, CancellationToken cancellationToken);
}

public sealed record OrganizationUser(Guid Id, string Username, string DisplayName, UserRole Role,
    bool IsActive, bool IsOwner, DateTimeOffset JoinedAtUtc, string? PhoneNumber, bool CanReopenDay = false);
