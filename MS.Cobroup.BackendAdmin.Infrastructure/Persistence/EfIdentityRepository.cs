using System.Data;
using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfIdentityRepository(IdentityDbContext dbContext) : IIdentityRepository
{
    public async Task<bool> TryBootstrapAsync(
        Organization organization,
        IReadOnlyCollection<ApplicationUser> users,
        IReadOnlyCollection<OrganizationMembership> memberships,
        SecurityAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (await dbContext.BootstrapMarkers.AnyAsync(cancellationToken))
        {
            return false;
        }

        dbContext.Add(BootstrapMarker.Complete(auditRecord.OccurredAtUtc));
        dbContext.Add(organization);
        dbContext.AddRange(users);
        dbContext.AddRange(memberships);
        dbContext.Add(auditRecord);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    public Task<ApplicationUser?> FindActiveUserAsync(
        string normalizedUsername,
        CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.NormalizedUsername == normalizedUsername && item.IsActive,
                cancellationToken);

    public async Task<OrganizationMembership?> SelectMostRecentlyAccessedMembershipAsync(
        Guid userId,
        DateTimeOffset accessedAtUtc,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.OrganizationMemberships
            .Include(item => item.Organization)
            .Where(item =>
                item.UserId == userId
                && item.IsActive
                && item.Organization.Status == OrganizationStatus.Active)
            .OrderByDescending(item => item.LastAccessedAtUtc.HasValue)
            .ThenByDescending(item => item.LastAccessedAtUtc)
            .ThenByDescending(item => item.JoinedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            return null;
        }

        membership.MarkAccessed(accessedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return membership;
    }

    public async Task<OrganizationMembership?> SelectMembershipAsync(
        Guid userId,
        Guid organizationId,
        DateTimeOffset accessedAtUtc,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.OrganizationMemberships
            .Include(item => item.User)
            .Include(item => item.Organization)
            .SingleOrDefaultAsync(
                item => item.UserId == userId
                    && item.OrganizationId == organizationId
                    && item.IsActive
                    && item.Organization.Status == OrganizationStatus.Active,
                cancellationToken);
        if (membership is null)
        {
            return null;
        }

        membership.MarkAccessed(accessedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return membership;
    }

    public async Task<IReadOnlyCollection<OrganizationMembership>> ListActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Include(item => item.Organization)
            .Where(item =>
                item.UserId == userId
                && item.IsActive
                && item.Organization.Status == OrganizationStatus.Active)
            .ToArrayAsync(cancellationToken);
}
