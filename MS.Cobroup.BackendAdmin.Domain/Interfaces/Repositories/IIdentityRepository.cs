using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

public interface IIdentityRepository
{
    Task<bool> TryBootstrapAsync(
        Organization organization,
        IReadOnlyCollection<ApplicationUser> users,
        IReadOnlyCollection<OrganizationMembership> memberships,
        SecurityAuditRecord auditRecord,
        CancellationToken cancellationToken);

    Task<ApplicationUser?> FindActiveUserAsync(
        string normalizedUsername,
        CancellationToken cancellationToken);

    Task<OrganizationMembership?> SelectMostRecentlyAccessedMembershipAsync(
        Guid userId,
        DateTimeOffset accessedAtUtc,
        CancellationToken cancellationToken);

    Task<OrganizationMembership?> SelectMembershipAsync(
        Guid userId,
        Guid organizationId,
        DateTimeOffset accessedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OrganizationMembership>> ListActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task SaveRefreshTokenAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken);

    Task<RefreshToken?> FindRefreshTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAtUtc,
        string reason,
        CancellationToken cancellationToken);

    Task<bool> RotateRefreshTokenAtomicAsync(
        string currentTokenHash,
        RefreshToken newToken,
        DateTimeOffset rotatedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> SwitchOrganizationSessionAtomicAsync(
        string currentTokenHash,
        Guid expectedUserId,
        Guid expectedCurrentOrgId,
        RefreshToken initialNewToken,
        DateTimeOffset switchedAtUtc,
        string reason,
        CancellationToken cancellationToken);

    Task RevokeRefreshTokenByHashAsync(
        string tokenHash,
        DateTimeOffset revokedAtUtc,
        string? replacedByTokenHash,
        string? reason,
        CancellationToken cancellationToken);

    Task RevokeUserRefreshTokensAsync(
        Guid userId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken);

    Task<int> RevokeRefreshTokensBySigningKeyIdAsync(
        string signingKeyId,
        DateTimeOffset revokedAtUtc,
        string reason,
        CancellationToken cancellationToken);

    Task<int> RevokeAllRefreshTokensAsync(
        DateTimeOffset revokedAtUtc,
        string reason,
        CancellationToken cancellationToken);
}

