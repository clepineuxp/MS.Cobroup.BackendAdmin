using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfUserManagementRepository(IdentityDbContext dbContext) : IUserManagementRepository
{
    public Task<bool> IsActiveAdminAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships.AsNoTracking().AnyAsync(membership =>
            membership.OrganizationId == organizationId && membership.UserId == userId
            && membership.Role == UserRole.Admin && membership.IsActive && membership.User.IsActive
            && membership.Organization.Status == OrganizationStatus.Active, cancellationToken);

    public async Task<IReadOnlyCollection<OrganizationUser>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await dbContext.OrganizationMemberships.AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId)
            .OrderBy(membership => membership.User.DisplayName).ThenBy(membership => membership.UserId)
            .Select(membership => new OrganizationUser(membership.UserId, membership.User.Username,
                membership.User.DisplayName, membership.Role, membership.IsActive && membership.User.IsActive,
                membership.IsOwner, membership.JoinedAtUtc, membership.User.PhoneNumber, membership.CanReopenDay))
            .ToArrayAsync(cancellationToken);

    public async Task<bool> TryCreateAsync(ApplicationUser user, OrganizationMembership membership,
        AdministrativeAuditRecord audit, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        dbContext.OrganizationMemberships.Add(membership);
        dbContext.AdministrativeAuditRecords.Add(audit);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_NormalizedUsername" })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
