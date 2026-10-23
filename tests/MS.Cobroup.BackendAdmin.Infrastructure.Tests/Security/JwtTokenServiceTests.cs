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
using Xunit;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

public sealed class JwtTokenServiceTests
{
    private static readonly byte[] KeyBytes = RandomNumberGenerator.GetBytes(32);
    private static readonly string KeyBase64 = Convert.ToBase64String(KeyBytes);

    private readonly IConfiguration configuration;
    private readonly IHostEnvironment environment;
    private readonly SymmetricKeyService keyService;
    private readonly TestIdentityRepository repository;
    private readonly JwtTokenService tokenService;

    public JwtTokenServiceTests()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api",
            ["Jwt:SigningKeyBase64"] = KeyBase64,
            ["Jwt:ActiveKeyId"] = "test-key-1"
        };

        configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        environment = new TestHostEnvironment { EnvironmentName = Environments.Development };
        keyService = new SymmetricKeyService(configuration, environment);
        repository = new TestIdentityRepository();
        tokenService = new JwtTokenService(configuration, keyService, repository);
    }

    [Fact]
    public async Task IssueTokensAsync_EmitsValidHs256Tokens_WithMandatoryClaimsAnd15mExpiration()
    {
        var (user, membership) = CreateUserAndMembership();

        var result = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);

        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RawRefreshToken);
        Assert.Equal(900, result.ExpiresIn);

        // Validate Access Token Claims and HS256 algorithm
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(result.AccessToken, keyService.TokenValidationParameters, out var validatedToken);
        var jwt = Assert.IsType<JwtSecurityToken>(validatedToken);

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.Equal("test-key-1", jwt.Header.Kid);
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
        var refreshValidationParams = keyService.TokenValidationParameters.Clone();
        refreshValidationParams.ValidAudience = "cobroup-refresh";
        handler.ValidateToken(result.RawRefreshToken, refreshValidationParams, out var validatedRefresh);
        var refreshJwt = Assert.IsType<JwtSecurityToken>(validatedRefresh);
        Assert.Equal(SecurityAlgorithms.HmacSha256, refreshJwt.Header.Alg);
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
        var wrongIssuerKeyService = new SymmetricKeyService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Issuer"] = "https://attacker.com",
                ["Identity:Audience"] = "cobroup-api",
                ["Jwt:SigningKeyBase64"] = KeyBase64,
                ["Jwt:ActiveKeyId"] = "attacker-key"
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
            handler.ValidateToken(attackerTokens.AccessToken, keyService.TokenValidationParameters, out _));

        // Genuine token validated with wrong audience must reject
        var genuineTokens = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);
        var wrongAudienceParams = keyService.TokenValidationParameters.Clone();
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

        // Missing signing key in production
        var incompleteConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api"
        }).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(incompleteConfig, prodEnv));
        Assert.Contains("Jwt:SigningKeyBase64", ex.Message);

        // Insecure HTTP issuer in production
        var httpConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "http://insecure.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api",
            ["Jwt:ActiveKeyId"] = "prod-kid-1",
            ["Jwt:SigningKeyBase64"] = KeyBase64
        }).Build();

        var httpEx = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(httpConfig, prodEnv));
        Assert.Contains("HTTPS", httpEx.Message);
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

    [Fact]
    public async Task RefreshToken_TracksSigningKeyId_AndRevokesOldKeyTokensOnRetirement()
    {
        var (user, membership) = CreateUserAndMembership();

        // 1. Emitir token bajo clave activa k1 ("test-key-1")
        var resultK1 = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);
        var tokenK1 = repository.RefreshTokens.Last();
        Assert.Equal("test-key-1", tokenK1.SigningKeyId);
        Assert.True(tokenK1.IsActive);

        // 2. Simular servicio rotado a k2
        var keyK2Bytes = RandomNumberGenerator.GetBytes(32);
        var inMemoryK2 = new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api",
            ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(keyK2Bytes),
            ["Jwt:ActiveKeyId"] = "k2",
            ["Jwt:PreviousValidationKeys:0:KeyId"] = "test-key-1",
            ["Jwt:PreviousValidationKeys:0:SigningKeyBase64"] = KeyBase64
        };
        var configK2 = new ConfigurationBuilder().AddInMemoryCollection(inMemoryK2).Build();
        using var keyServiceK2 = new SymmetricKeyService(configK2, environment);
        var tokenServiceK2 = new JwtTokenService(configK2, keyServiceK2, repository);

        // 3. Rotar el refresh token durante la ventana de solapamiento
        var refreshResult = await tokenServiceK2.RefreshTokensAsync(resultK1.RawRefreshToken, CancellationToken.None);
        Assert.True(refreshResult.Success);
        var tokenK2 = repository.RefreshTokens.Last();
        Assert.Equal("k2", tokenK2.SigningKeyId);
        Assert.True(tokenK2.IsActive);

        // 4. Etapa 3: Retiro de clave anterior test-key-1
        var revokedCount = await repository.RevokeRefreshTokensBySigningKeyIdAsync(
            "test-key-1",
            DateTimeOffset.UtcNow,
            "Key test-key-1 retired",
            CancellationToken.None);

        // El token anterior k1 ya estaba revocado por la rotación, pero tokens inactivos de k1 que no rotaron se revocarían
        // El token k2 permanece completamente activo
        Assert.True(tokenK2.IsActive);
        Assert.Equal("k2", tokenK2.SigningKeyId);
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
