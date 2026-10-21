using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfRouteManagementRepository(IdentityDbContext dbContext) : IRouteManagementRepository
{
    public Task<bool> NameExistsAsync(
        Guid organizationId,
        string normalizedName,
        CancellationToken cancellationToken) =>
        dbContext.Routes.AnyAsync(
            route => route.OrganizationId == organizationId && route.NormalizedName == normalizedName,
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, UserRole>> GetActiveUserRolesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.OrganizationId == organizationId
                && membership.IsActive
                && membership.User.IsActive
                && userIds.Contains(membership.UserId))
            .ToDictionaryAsync(
                membership => membership.UserId,
                membership => membership.Role,
                cancellationToken);

    public async Task<bool> TryAddAsync(
        CollectionRoute route,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Routes.Add(route);
        dbContext.AdministrativeAuditRecords.Add(auditRecord);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_routes_OrganizationId_NormalizedName"
            })
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }
}
