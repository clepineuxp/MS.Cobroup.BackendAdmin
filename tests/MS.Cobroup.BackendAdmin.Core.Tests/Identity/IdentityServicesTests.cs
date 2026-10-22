using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Identity;

public sealed class IdentityServicesTests
{
    [Fact]
    public async Task Bootstrap_CreatesOneOrganizationAndOneMembershipPerRole()
    {
        var repository = new MemoryIdentityRepository();
        var service = new BootstrapIdentityService(repository, new DeterministicPasswordService());

        var first = await service.ExecuteAsync(CreateCommand(), CancellationToken.None);
        var second = await service.ExecuteAsync(CreateCommand(), CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal("ACME", repository.Organization?.Code);
        Assert.Equal(3, repository.Users.Count);
        Assert.Equal(3, repository.Memberships.Count);
        Assert.Contains(repository.Memberships, membership => membership.Role is UserRole.Admin);
        Assert.Contains(repository.Memberships, membership => membership.Role is UserRole.Supervisor);
        Assert.Contains(repository.Memberships, membership => membership.Role is UserRole.Collector);
    }

    [Fact]
    public async Task Authenticate_DoesNotRequireOrganizationAndSelectsLastAccessedMembership()
    {
        var repository = new MemoryIdentityRepository();
        var passwords = new DeterministicPasswordService();
        var bootstrap = new BootstrapIdentityService(repository, passwords);
        await bootstrap.ExecuteAsync(CreateCommand(), CancellationToken.None);
        var authentication = new AuthenticateUserService(repository, passwords);

        var result = await authentication.ExecuteAsync("ADMIN", "a-strong-password", CancellationToken.None);
        var invalid = await authentication.ExecuteAsync("admin", "wrong-password", CancellationToken.None);

        Assert.Equal(AuthenticationOutcome.Authenticated, result.Outcome);
        Assert.Equal(repository.Organization?.Id, result.Membership?.OrganizationId);
        Assert.Equal(AuthenticationOutcome.InvalidCredentials, invalid.Outcome);
    }

    [Fact]
    public async Task Authenticate_ReportsMembershipRequiredWhenUserHasNoActiveOrganization()
    {
        var repository = new MemoryIdentityRepository();
        var passwords = new DeterministicPasswordService();
        var user = ApplicationUser.Create("standalone", "Standalone", passwords.Hash("password"), DateTimeOffset.UtcNow);
        repository.Users.Add(user);
        var authentication = new AuthenticateUserService(repository, passwords);

        var result = await authentication.ExecuteAsync("standalone", "password", CancellationToken.None);

        Assert.Equal(AuthenticationOutcome.MembershipRequired, result.Outcome);
        Assert.Equal(user.Id, result.User?.Id);
    }

    [Fact]
    public async Task Authenticate_SelectsTheOrganizationAccessedMostRecently()
    {
        var repository = new MemoryIdentityRepository();
        var passwords = new DeterministicPasswordService();
        await new BootstrapIdentityService(repository, passwords)
            .ExecuteAsync(CreateCommand(), CancellationToken.None);
        var administrator = repository.Users.Single(user => user.NormalizedUsername == "ADMIN");
        var secondOrganization = Organization.Create("SECOND", "Second", DateTimeOffset.UtcNow);
        var secondMembership = OrganizationMembership.Create(
            administrator.Id,
            secondOrganization.Id,
            UserRole.Admin,
            DateTimeOffset.UtcNow);
        secondMembership.MarkAccessed(DateTimeOffset.UtcNow.AddMinutes(1));
        repository.AddMembership(secondMembership, secondOrganization);

        var result = await new AuthenticateUserService(repository, passwords)
            .ExecuteAsync("admin", "a-strong-password", CancellationToken.None);

        Assert.Equal(secondOrganization.Id, result.Membership?.OrganizationId);
    }

    private sealed class DeterministicPasswordService : IPasswordCredentialService
    {
        public string Hash(string password) => $"hashed::{password}";

        public bool Verify(ApplicationUser user, string password) => user.PasswordHash == $"hashed::{password}";
    }

    private sealed class MemoryIdentityRepository : IIdentityRepository
    {
        public Organization? Organization { get; private set; }

        public List<ApplicationUser> Users { get; } = [];

        public List<OrganizationMembership> Memberships { get; } = [];

        public void AddMembership(OrganizationMembership membership, Organization organization)
        {
            Memberships.Add(membership);
            SetOrganizationNavigation([membership], organization);
        }

        public Task<bool> TryBootstrapAsync(
            Organization organization,
            IReadOnlyCollection<ApplicationUser> users,
            IReadOnlyCollection<OrganizationMembership> memberships,
            SecurityAuditRecord auditRecord,
            CancellationToken cancellationToken)
        {
            if (Organization is not null)
            {
                return Task.FromResult(false);
            }

            Organization = organization;
            Users.AddRange(users);
            Memberships.AddRange(memberships);
            SetOrganizationNavigation(memberships, organization);
            return Task.FromResult(true);
        }

        public Task<ApplicationUser?> FindActiveUserAsync(
            string normalizedUsername,
            CancellationToken cancellationToken) =>
            Task.FromResult(Users.SingleOrDefault(user => user.NormalizedUsername == normalizedUsername));

        public Task<OrganizationMembership?> SelectMostRecentlyAccessedMembershipAsync(
            Guid userId,
            DateTimeOffset accessedAtUtc,
            CancellationToken cancellationToken)
        {
            var membership = Memberships
                .Where(item => item.UserId == userId && item.IsActive)
                .OrderByDescending(item => item.LastAccessedAtUtc)
                .ThenByDescending(item => item.JoinedAtUtc)
                .FirstOrDefault();
            membership?.MarkAccessed(accessedAtUtc);
            return Task.FromResult(membership);
        }

        public Task<OrganizationMembership?> SelectMembershipAsync(
            Guid userId,
            Guid organizationId,
            DateTimeOffset accessedAtUtc,
            CancellationToken cancellationToken)
        {
            var membership = Memberships.SingleOrDefault(item =>
                item.UserId == userId && item.OrganizationId == organizationId && item.IsActive);
            membership?.MarkAccessed(accessedAtUtc);
            return Task.FromResult(membership);
        }

        public Task<IReadOnlyCollection<OrganizationMembership>> ListActiveMembershipsAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<OrganizationMembership> result = Memberships
                .Where(item => item.UserId == userId && item.IsActive)
                .ToArray();
            return Task.FromResult(result);
        }

        public List<RefreshToken> RefreshTokens { get; } = [];

        public Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            RefreshTokens.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(RefreshTokens.SingleOrDefault(t => t.TokenHash == tokenHash));

        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAtUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var token in RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null))
            {
                token.Revoke(revokedAtUtc, reason: reason);
            }
            return Task.CompletedTask;
        }

        public Task<bool> RotateRefreshTokenAtomicAsync(
            string currentTokenHash,
            RefreshToken newToken,
            DateTimeOffset rotatedAtUtc,
            CancellationToken cancellationToken)
        {
            var existing = RefreshTokens.SingleOrDefault(t => t.TokenHash == currentTokenHash);
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
            var existing = RefreshTokens.SingleOrDefault(t => t.TokenHash == currentTokenHash);
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

        public Task RevokeRefreshTokenByHashAsync(
            string tokenHash,
            DateTimeOffset revokedAtUtc,
            string? replacedByTokenHash,
            string? reason,
            CancellationToken cancellationToken)
        {
            var existing = RefreshTokens.SingleOrDefault(t => t.TokenHash == tokenHash);
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

        private static void SetOrganizationNavigation(
            IReadOnlyCollection<OrganizationMembership> memberships,
            Organization organization)
        {
            var property = typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.Organization));
            foreach (var membership in memberships)
            {
                property!.SetValue(membership, organization);
            }
        }
    }

    private static BootstrapIdentityCommand CreateCommand() => new(
        " acme ",
        "Acme",
        new BootstrapUserCommand(" admin ", "Administrator", "a-strong-password"),
        new BootstrapUserCommand("supervisor", "Supervisor", "another-strong-password"),
        new BootstrapUserCommand("collector", "Collector", "collector-strong-password"),
        "127.0.0.1");
}
