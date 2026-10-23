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
            .Include(item => item.User)
            .Include(item => item.Organization)
            .Where(item =>
                item.UserId == userId
                && item.IsActive
                && item.User.IsActive
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
                    && item.User.IsActive
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

    public async Task SaveRefreshTokenAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshToken?> FindRefreshTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        return await dbContext.RefreshTokens
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
    }

    public async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(item => item.FamilyId == familyId && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(revokedAtUtc, reason: reason);
        }

        if (tokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> RotateRefreshTokenAtomicAsync(
        string currentTokenHash,
        RefreshToken newToken,
        DateTimeOffset rotatedAtUtc,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            RefreshToken? existing;
            if (dbContext.Database.IsNpgsql())
            {
                existing = await dbContext.RefreshTokens
                    .FromSqlRaw("SELECT * FROM refresh_tokens WHERE \"TokenHash\" = {0} FOR UPDATE", currentTokenHash)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                existing = await dbContext.RefreshTokens
                    .FirstOrDefaultAsync(item => item.TokenHash == currentTokenHash, cancellationToken);
            }

            if (existing is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (!existing.IsActive)
            {
                // Replay detected! Revoke the entire family
                var familyTokens = await dbContext.RefreshTokens
                    .Where(item => item.FamilyId == existing.FamilyId && item.RevokedAtUtc == null)
                    .ToListAsync(cancellationToken);

                foreach (var token in familyTokens)
                {
                    token.Revoke(rotatedAtUtc, reason: "Replay attack detected during rotation");
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            existing.Revoke(rotatedAtUtc, replacedByTokenHash: newToken.TokenHash, reason: "Rotated");
            dbContext.RefreshTokens.Add(newToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task<bool> SwitchOrganizationSessionAtomicAsync(
        string currentTokenHash,
        Guid expectedUserId,
        Guid expectedCurrentOrgId,
        RefreshToken initialNewToken,
        DateTimeOffset switchedAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            RefreshToken? existing;
            if (dbContext.Database.IsNpgsql())
            {
                existing = await dbContext.RefreshTokens
                    .FromSqlRaw("SELECT * FROM refresh_tokens WHERE \"TokenHash\" = {0} FOR UPDATE", currentTokenHash)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                existing = await dbContext.RefreshTokens
                    .FirstOrDefaultAsync(item => item.TokenHash == currentTokenHash, cancellationToken);
            }

            if (existing is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            // Check sub matches access token user and tenant matches current active tenant
            if (existing.UserId != expectedUserId || existing.OrganizationId != expectedCurrentOrgId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (!existing.IsActive)
            {
                // Replay or revoked token detected: revoke entire family to prevent session hijacking
                var familyTokens = await dbContext.RefreshTokens
                    .Where(item => item.FamilyId == existing.FamilyId && item.RevokedAtUtc == null)
                    .ToListAsync(cancellationToken);

                foreach (var token in familyTokens)
                {
                    token.Revoke(switchedAtUtc, reason: "Replay attack detected during organization switch");
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            // Revoke old family atomically
            var activeTokensInFamily = await dbContext.RefreshTokens
                .Where(item => item.FamilyId == existing.FamilyId && item.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokensInFamily)
            {
                token.Revoke(switchedAtUtc, replacedByTokenHash: initialNewToken.TokenHash, reason: reason);
            }

            // Persist initial new refresh token under the new family
            dbContext.RefreshTokens.Add(initialNewToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task RevokeRefreshTokenByHashAsync(
        string tokenHash,
        DateTimeOffset revokedAtUtc,
        string? replacedByTokenHash,
        string? reason,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (existing is not null)
        {
            existing.Revoke(revokedAtUtc, replacedByTokenHash, reason);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeUserRefreshTokensAsync(
        Guid userId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(item => item.UserId == userId && item.RevokedAtUtc == null && item.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(revokedAtUtc, reason: "Revoked for user");
        }

        if (activeTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> RevokeRefreshTokensBySigningKeyIdAsync(
        string signingKeyId,
        DateTimeOffset revokedAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(item => item.SigningKeyId == signingKeyId && item.RevokedAtUtc == null && item.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(revokedAtUtc, reason: reason);
        }

        if (activeTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return activeTokens.Count;
    }

    public async Task<int> RevokeAllRefreshTokensAsync(
        DateTimeOffset revokedAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(item => item.RevokedAtUtc == null && item.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(revokedAtUtc, reason: reason);
        }

        if (activeTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return activeTokens.Count;
    }
}
