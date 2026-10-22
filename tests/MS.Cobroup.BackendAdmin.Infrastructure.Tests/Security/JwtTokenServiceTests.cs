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

    [Fact]
    public async Task SwitchOrganizationSessionAsync_Valid_RevokesOldFamily_IssuesNewTokens()
    {
        var (user, membership1) = CreateUserAndMembership();
        var now = DateTimeOffset.UtcNow;
        var org2 = Organization.Create("ORG02", "Organization Two", now);
        var membership2 = OrganizationMembership.Create(user.Id, org2.Id, UserRole.Admin, now);
        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.Organization))!.SetValue(membership2, org2);
        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.User))!.SetValue(membership2, user);
        repository.Organizations.Add(org2);
        repository.Memberships.Add(membership2);

        var initialTokens = await tokenService.IssueTokensAsync(user, membership1, null, CancellationToken.None);
        var oldFamilyId = initialTokens.FamilyId;

        var switchResult = await tokenService.SwitchOrganizationSessionAsync(
            user.Id,
            membership1.OrganizationId,
            initialTokens.RawRefreshToken,
            org2.Id,
            CancellationToken.None);

        Assert.True(switchResult.Success);
        Assert.NotNull(switchResult.AccessToken);
        Assert.NotNull(switchResult.RawRefreshToken);
        Assert.NotEqual(initialTokens.RawRefreshToken, switchResult.RawRefreshToken);

        // Verify old family revoked
        var oldTokens = repository.RefreshTokens.Where(t => t.FamilyId == oldFamilyId).ToList();
        Assert.All(oldTokens, t => Assert.NotNull(t.RevokedAtUtc));

        // Verify old token cannot refresh
        var refreshOld = await tokenService.RefreshTokensAsync(initialTokens.RawRefreshToken, CancellationToken.None);
        Assert.False(refreshOld.Success);

        // Verify new token CAN refresh
        var refreshNew = await tokenService.RefreshTokensAsync(switchResult.RawRefreshToken, CancellationToken.None);
        Assert.True(refreshNew.Success);
    }

    [Fact]
    public async Task SwitchOrganizationSessionAsync_WrongUserOrCurrentTenant_FailsWithoutMutating()
    {
        var (user, membership1) = CreateUserAndMembership();
        var initialTokens = await tokenService.IssueTokensAsync(user, membership1, null, CancellationToken.None);

        // Wrong user
        var wrongUserResult = await tokenService.SwitchOrganizationSessionAsync(
            Guid.NewGuid(),
            membership1.OrganizationId,
            initialTokens.RawRefreshToken,
            membership1.OrganizationId,
            CancellationToken.None);
        Assert.False(wrongUserResult.Success);
        Assert.Equal(403, wrongUserResult.StatusCode);

        // Wrong current tenant
        var wrongTenantResult = await tokenService.SwitchOrganizationSessionAsync(
            user.Id,
            Guid.NewGuid(),
            initialTokens.RawRefreshToken,
            membership1.OrganizationId,
            CancellationToken.None);
        Assert.False(wrongTenantResult.Success);
        Assert.Equal(403, wrongTenantResult.StatusCode);
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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "BackendAdmin.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
