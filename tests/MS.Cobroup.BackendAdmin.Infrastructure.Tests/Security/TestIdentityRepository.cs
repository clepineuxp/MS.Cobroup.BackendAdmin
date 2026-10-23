using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

internal sealed class TestIdentityRepository : IIdentityRepository
{
    public List<ApplicationUser> Users { get; } = [];
    public List<Organization> Organizations { get; } = [];
    public List<OrganizationMembership> Memberships { get; } = [];
    public List<RefreshToken> RefreshTokens { get; } = [];

    public Task<bool> TryBootstrapAsync(
        Organization organization,
        IReadOnlyCollection<ApplicationUser> users,
        IReadOnlyCollection<OrganizationMembership> memberships,
        SecurityAuditRecord auditRecord,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<ApplicationUser?> FindActiveUserAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => u.NormalizedUsername == normalizedUsername));

    public Task<OrganizationMembership?> SelectMostRecentlyAccessedMembershipAsync(Guid userId, DateTimeOffset accessedAtUtc, CancellationToken cancellationToken) =>
        Task.FromResult(Memberships.FirstOrDefault(m => m.UserId == userId && m.IsActive));

    public Task<OrganizationMembership?> SelectMembershipAsync(Guid userId, Guid organizationId, DateTimeOffset accessedAtUtc, CancellationToken cancellationToken) =>
        Task.FromResult(Memberships.FirstOrDefault(m => m.UserId == userId && m.OrganizationId == organizationId && m.IsActive));

    public Task<IReadOnlyCollection<OrganizationMembership>> ListActiveMembershipsAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<OrganizationMembership>>(Memberships.Where(m => m.UserId == userId && m.IsActive).ToList());

    public Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        RefreshTokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAtUtc, string reason, CancellationToken cancellationToken)
    {
        foreach (var token in RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null))
        {
            token.Revoke(revokedAtUtc, reason: reason);
        }
        return Task.CompletedTask;
    }

    public Task<bool> RotateRefreshTokenAtomicAsync(string currentTokenHash, RefreshToken newToken, DateTimeOffset rotatedAtUtc, CancellationToken cancellationToken)
    {
        var existing = RefreshTokens.FirstOrDefault(t => t.TokenHash == currentTokenHash);
        if (existing is null) return Task.FromResult(false);

        if (!existing.IsActive)
        {
            foreach (var token in RefreshTokens.Where(t => t.FamilyId == existing.FamilyId && t.RevokedAtUtc == null))
            {
                token.Revoke(rotatedAtUtc, reason: "Replay attack detected during rotation");
            }
            return Task.FromResult(false);
        }

        existing.Revoke(rotatedAtUtc, replacedByTokenHash: newToken.TokenHash, reason: "Rotated");
        RefreshTokens.Add(newToken);
        return Task.FromResult(true);
    }

    public Task<bool> SwitchOrganizationSessionAtomicAsync(
        string currentTokenHash,
        Guid expectedUserId,
        Guid expectedCurrentOrgId,
        RefreshToken initialNewToken,
        DateTimeOffset switchedAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var existing = RefreshTokens.FirstOrDefault(t => t.TokenHash == currentTokenHash);
        if (existing is null) return Task.FromResult(false);

        if (existing.UserId != expectedUserId || existing.OrganizationId != expectedCurrentOrgId)
        {
            return Task.FromResult(false);
        }

        if (!existing.IsActive)
        {
            foreach (var token in RefreshTokens.Where(t => t.FamilyId == existing.FamilyId && t.RevokedAtUtc == null))
            {
                token.Revoke(switchedAtUtc, reason: "Replay attack detected during organization switch");
            }
            return Task.FromResult(false);
        }

        foreach (var token in RefreshTokens.Where(t => t.FamilyId == existing.FamilyId && t.RevokedAtUtc == null))
        {
            token.Revoke(switchedAtUtc, replacedByTokenHash: initialNewToken.TokenHash, reason: reason);
        }

        RefreshTokens.Add(initialNewToken);
        return Task.FromResult(true);
    }

    public Task RevokeRefreshTokenByHashAsync(string tokenHash, DateTimeOffset revokedAtUtc, string? replacedByTokenHash, string? reason, CancellationToken cancellationToken)
    {
        var existing = RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        existing?.Revoke(revokedAtUtc, replacedByTokenHash, reason);
        return Task.CompletedTask;
    }

    public Task RevokeUserRefreshTokensAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
    {
        foreach (var token in RefreshTokens.Where(t => t.UserId == userId && t.IsActive))
        {
            token.Revoke(revokedAtUtc, reason: "Revoked for user");
        }
        return Task.CompletedTask;
    }

    public Task<int> RevokeRefreshTokensBySigningKeyIdAsync(string signingKeyId, DateTimeOffset revokedAtUtc, string reason, CancellationToken cancellationToken)
    {
        var active = RefreshTokens.Where(t => t.SigningKeyId == signingKeyId && t.IsActive).ToList();
        foreach (var token in active)
        {
            token.Revoke(revokedAtUtc, reason: reason);
        }
        return Task.FromResult(active.Count);
    }

    public Task<int> RevokeAllRefreshTokensAsync(DateTimeOffset revokedAtUtc, string reason, CancellationToken cancellationToken)
    {
        var active = RefreshTokens.Where(t => t.IsActive).ToList();
        foreach (var token in active)
        {
            token.Revoke(revokedAtUtc, reason: reason);
        }
        return Task.FromResult(active.Count);
    }
}
