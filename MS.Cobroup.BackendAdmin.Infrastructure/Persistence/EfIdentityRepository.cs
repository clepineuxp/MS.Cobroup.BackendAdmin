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
        string organizationCode,
        string normalizedUsername,
        CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .Include(item => item.Organization)
            .SingleOrDefaultAsync(
                item => item.Organization.Code == organizationCode
                    && item.NormalizedUsername == normalizedUsername
                    && item.IsActive
                    && item.Organization.Status == OrganizationStatus.Active,
                cancellationToken);
}
