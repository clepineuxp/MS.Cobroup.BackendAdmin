using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Core.Users;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfUserDirectoryRepository(IdentityDbContext dbContext) : IUserDirectoryRepository
{
    public async Task<IReadOnlyCollection<AssignableUser>> ListAssignableAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.OrganizationId == organizationId
                && membership.IsActive
                && membership.User.IsActive
                && (membership.Role == UserRole.Supervisor || membership.Role == UserRole.Collector))
            .OrderBy(membership => membership.Role)
            .ThenBy(membership => membership.User.DisplayName)
            .Select(membership => new AssignableUser(
                membership.UserId,
                membership.User.Username,
                membership.User.DisplayName,
                membership.Role))
            .ToArrayAsync(cancellationToken);
}
