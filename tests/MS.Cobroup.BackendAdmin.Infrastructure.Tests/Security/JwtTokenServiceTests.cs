using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

public sealed class JwtTokenServiceTests
{
    private readonly IConfiguration configuration;
    private readonly IHostEnvironment environment;
    private readonly RsaKeyService rsaKeyService;
    private readonly TestIdentityRepository repository;
    private readonly JwtTokenService tokenService;

    public JwtTokenServiceTests()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api",
            ["Jwt:KeyId"] = "test-rsa-key-1"
        };

        configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        environment = new TestHostEnvironment { EnvironmentName = Environments.Development };
        rsaKeyService = new RsaKeyService(configuration, environment);
        repository = new TestIdentityRepository();
        tokenService = new JwtTokenService(configuration, rsaKeyService, repository);
    }

    [Fact]
    public async Task IssueTokensAsync_EmitsValidRs256Tokens_WithMandatoryClaimsAnd15mExpiration()
    {
        var (user, membership) = CreateUserAndMembership();

        var result = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);

        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RawRefreshToken);
        Assert.Equal(900, result.ExpiresIn);

        // Validate Access Token Claims and RS256 algorithm
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(result.AccessToken, rsaKeyService.TokenValidationParameters, out var validatedToken);
        var jwt = Assert.IsType<JwtSecurityToken>(validatedToken);

        Assert.Equal(SecurityAlgorithms.RsaSha256, jwt.Header.Alg);
        Assert.Equal("test-rsa-key-1", jwt.Header.Kid);
        Assert.Equal("access", jwt.Claims.First(c => c.Type == "typ").Value);
        Assert.Equal("https://identity.cobroup.internal", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Iss).Value);
        Assert.Equal("cobroup-api", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Aud).Value);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(membership.OrganizationId.ToString(), jwt.Claims.First(c => c.Type == "organization_id").Value);
        Assert.Equal(membership.Role.ToString(), jwt.Claims.First(c => c.Type == "role").Value);

        // Expiration must be <= 15 minutes (900 seconds)
        var iat = long.Parse(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Iat).Value);
        var exp = long.Parse(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Exp).Value);
        Assert.True(exp - iat <= 900);

        // Verify Refresh Token Claims
        var refreshValidationParams = rsaKeyService.TokenValidationParameters.Clone();
        refreshValidationParams.ValidAudience = "cobroup-refresh";
        handler.ValidateToken(result.RawRefreshToken, refreshValidationParams, out var validatedRefresh);
        var refreshJwt = Assert.IsType<JwtSecurityToken>(validatedRefresh);
        Assert.Equal("refresh", refreshJwt.Claims.First(c => c.Type == "typ").Value);
        Assert.Equal("cobroup-refresh", refreshJwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Aud).Value);
        Assert.Equal(result.FamilyId.ToString(), refreshJwt.Claims.First(c => c.Type == "family_id").Value);

        // Verify server-side stores ONLY the SHA-256 hash, NOT the raw token
        var savedRecord = Assert.Single(repository.RefreshTokens);
        Assert.NotEqual(result.RawRefreshToken, savedRecord.TokenHash);
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(result.RawRefreshToken.Trim())));
        Assert.Equal(expectedHash, savedRecord.TokenHash);
        Assert.True(savedRecord.IsActive);
    }

    [Fact]
    public async Task ValidateToken_RejectsWrongIssuerOrAudience()
    {
        var (user, membership) = CreateUserAndMembership();
        var handler = new JwtSecurityTokenHandler();

        // Token with wrong issuer
        var wrongIssuerKeyService = new RsaKeyService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Issuer"] = "https://attacker.com",
                ["Identity:Audience"] = "cobroup-api",
                ["Jwt:KeyId"] = "attacker-key"
            }).Build(),
            environment);

        var wrongTokenService = new JwtTokenService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Issuer"] = "https://attacker.com",
                ["Identity:Audience"] = "cobroup-api"
            }).Build(),
            wrongIssuerKeyService,
            repository);

        var attackerTokens = await wrongTokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);

        // Validating with genuine params must reject wrong issuer
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(attackerTokens.AccessToken, rsaKeyService.TokenValidationParameters, out _));

        // Genuine token validated with wrong audience must reject
        var genuineTokens = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);
        var wrongAudienceParams = rsaKeyService.TokenValidationParameters.Clone();
        wrongAudienceParams.ValidAudience = "different-api";

        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            handler.ValidateToken(genuineTokens.AccessToken, wrongAudienceParams, out _));
    }

    [Fact]
    public async Task RefreshTokensAsync_RotatesTokenAndRevokesParent()
    {
        var (user, membership) = CreateUserAndMembership();
        var initial = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);

        var refreshed = await tokenService.RefreshTokensAsync(initial.RawRefreshToken, CancellationToken.None);

        Assert.True(refreshed.Success);
        Assert.NotNull(refreshed.AccessToken);
        Assert.NotNull(refreshed.RawRefreshToken);
        Assert.NotEqual(initial.RawRefreshToken, refreshed.RawRefreshToken);

        // Initial token is now revoked
        var initialHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(initial.RawRefreshToken.Trim())));
        var initialRecord = repository.RefreshTokens.Single(t => t.TokenHash == initialHash);
        Assert.False(initialRecord.IsActive);
        Assert.NotNull(initialRecord.RevokedAtUtc);

        // New token is active
        var newHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshed.RawRefreshToken.Trim())));
        var newRecord = repository.RefreshTokens.Single(t => t.TokenHash == newHash);
        Assert.True(newRecord.IsActive);
        Assert.Equal(initial.FamilyId, newRecord.FamilyId);
    }

    [Fact]
    public async Task RefreshTokensAsync_ReplayAttack_RevokesEntireFamily()
    {
        var (user, membership) = CreateUserAndMembership();
        var initial = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);

        // Legitimate rotation: initial -> second
        var second = await tokenService.RefreshTokensAsync(initial.RawRefreshToken, CancellationToken.None);
        Assert.True(second.Success);

        // Replay attack: Attacker tries to use 'initial' token again!
        var replayAttempt = await tokenService.RefreshTokensAsync(initial.RawRefreshToken, CancellationToken.None);

        Assert.False(replayAttempt.Success);
        Assert.Contains("reutilizado", replayAttempt.Error ?? "");

        // Entire family must now be revoked
        var familyTokens = repository.RefreshTokens.Where(t => t.FamilyId == initial.FamilyId).ToList();
        Assert.Equal(2, familyTokens.Count);
        Assert.All(familyTokens, token => Assert.False(token.IsActive));
        Assert.All(familyTokens, token => Assert.NotNull(token.RevokedAtUtc));

        // Even the legitimately rotated 'second' token can no longer be refreshed!
        var secondAttempt = await tokenService.RefreshTokensAsync(second.RawRefreshToken!, CancellationToken.None);
        Assert.False(secondAttempt.Success);
    }

    [Fact]
    public void FailFastOutsideDevelopment_ThrowsWhenConfigurationIncomplete()
    {
        var prodEnv = new TestHostEnvironment { EnvironmentName = Environments.Production };

        // Missing private key in production
        var incompleteConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api"
        }).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new RsaKeyService(incompleteConfig, prodEnv));
        Assert.Contains("Jwt:PrivateKeyPem is required outside Development", ex.Message);

        // Insecure HTTP issuer in production
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportRSAPrivateKeyPem();
        var httpConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "http://insecure.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api",
            ["Jwt:KeyId"] = "prod-kid-1",
            ["Jwt:PrivateKeyPem"] = pem
        }).Build();

        var httpEx = Assert.Throws<InvalidOperationException>(() => new RsaKeyService(httpConfig, prodEnv));
        Assert.Contains("HTTPS URL", httpEx.Message);
    }

    [Fact]
    public void JwksEndpoint_ExposesOnlyPublicParameters()
    {
        var jwks = rsaKeyService.GetPublicJwks();

        Assert.NotNull(jwks);
        var key = Assert.Single(jwks.Keys);
        Assert.Equal("RSA", key.Kty);
        Assert.Equal("sig", key.Use);
        Assert.Equal(SecurityAlgorithms.RsaSha256, key.Alg);
        Assert.Equal("test-rsa-key-1", key.Kid);
        Assert.NotNull(key.N);
        Assert.NotNull(key.E);

        // Private parameters must never be exported!
        Assert.Null(key.D);
        Assert.Null(key.DP);
        Assert.Null(key.DQ);
        Assert.Null(key.QI);
        Assert.Null(key.P);
        Assert.Null(key.Q);
    }

    private (ApplicationUser User, OrganizationMembership Membership) CreateUserAndMembership()
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create("collector1", "Collector One", "hashed_pwd", now);
        var org = Organization.Create("ORG01", "Organization One", now);
        var membership = OrganizationMembership.Create(user.Id, org.Id, UserRole.Collector, now);
        var prop = typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.Organization));
        prop!.SetValue(membership, org);
        var userProp = typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.User));
        userProp!.SetValue(membership, user);

        repository.Users.Add(user);
        repository.Organizations.Add(org);
        repository.Memberships.Add(membership);

        return (user, membership);
    }

    private sealed class TestIdentityRepository : IIdentityRepository
    {
        public List<ApplicationUser> Users { get; } = [];
        public List<Organization> Organizations { get; } = [];
        public List<OrganizationMembership> Memberships { get; } = [];
        public List<RefreshToken> RefreshTokens { get; } = [];

        public Task<bool> TryBootstrapAsync(Organization organization, IReadOnlyCollection<ApplicationUser> users, IReadOnlyCollection<OrganizationMembership> memberships, SecurityAuditRecord auditRecord, CancellationToken cancellationToken) =>
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
                // Replay detected!
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
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "BackendAdmin.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
