using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfUserEditingRepository(IdentityDbContext dbContext) : IUserEditingRepository
{
    public Task<OrganizationMembership?> FindAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships.Include(item => item.User)
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId, cancellationToken);

    public Task<bool> HasOtherMembershipsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships.AnyAsync(item => item.UserId == userId && item.OrganizationId != organizationId, cancellationToken);

    public Task<bool> HasActiveRouteAssignmentsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Routes.AnyAsync(route => route.OrganizationId == organizationId && route.Status == CollectionRouteStatus.Active
            && (route.SupervisorUserId == userId || route.CollectorAssignments.Any(item => item.CollectorUserId == userId)), cancellationToken);

    public async Task<bool> SaveAsync(OrganizationMembership membership, AdministrativeAuditRecord audit,
        bool revokeAllSessions, bool revokeTenantSessions, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (revokeAllSessions || revokeTenantSessions)
        {
            var tokens = await dbContext.RefreshTokens.Where(token => token.UserId == membership.UserId && token.RevokedAtUtc == null
                && (revokeAllSessions || token.OrganizationId == membership.OrganizationId)).ToArrayAsync(cancellationToken);
            foreach (var token in tokens) token.Revoke(audit.OccurredAtUtc, reason: "user.updated");
        }
        dbContext.AdministrativeAuditRecords.Add(audit);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_NormalizedUsername" })
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
