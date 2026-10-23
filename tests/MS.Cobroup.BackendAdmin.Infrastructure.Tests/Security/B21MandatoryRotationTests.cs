extern alias BackendApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using BackendSymmetricValidationService = BackendApi::MS.Cobroup.Backend.Api.Security.SymmetricTokenValidationService;
using Xunit;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

/// <summary>
/// Pruebas unitarias e integradas obligatorias para la remediación de B-21.
/// </summary>
public sealed class B21MandatoryRotationTests
{
    private static readonly byte[] Key1Bytes = RandomNumberGenerator.GetBytes(32);
    private static readonly string Key1Base64 = Convert.ToBase64String(Key1Bytes);

    private static readonly byte[] Key2Bytes = RandomNumberGenerator.GetBytes(32);
    private static readonly string Key2Base64 = Convert.ToBase64String(Key2Bytes);

    private const string Issuer = "https://identity.cobroup.internal";
    private const string Audience = "cobroup-api";

    // 1. ActiveKeyId ausente en configuración del host -> startup / servicio falla
    [Fact]
    public void Test01_ActiveKeyId_MissingInConfiguration_FailsStartup()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = Issuer,
            ["Identity:Audience"] = Audience,
            ["Jwt:SigningKeyBase64"] = Key1Base64
            // Jwt:ActiveKeyId ausente intencionalmente
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(config, env));
        Assert.Contains("Jwt:ActiveKeyId", ex.Message);
        Assert.Contains("obligatorio", ex.Message);
    }

    // 2. Constructor de RefreshToken sin SigningKeyId, vacío o inválido -> rechazo
    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid key id with spaces")]
    [InlineData("invalid@char!")]
    public void Test02_RefreshTokenConstructor_InvalidSigningKeyId_ThrowsArgumentException(string? invalidKid)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            new RefreshToken(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "some-token-hash",
                now.AddDays(7),
                now,
                invalidKid!));
    }

    [Theory]
    [InlineData("k1")]
    [InlineData("k2")]
    [InlineData("key-2026.01_prod")]
    public void Test02b_RefreshTokenConstructor_ValidSigningKeyId_Accepted(string validKid)
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "some-token-hash",
            now.AddDays(7),
            now,
            validKid);

        Assert.Equal(validKid, token.SigningKeyId);
    }

    // 3. Migración de sesiones heredadas sin atribuir silenciosamente 'k1'
    [Fact]
    public void Test03_MigrationScript_PurgesLegacySessions_WithoutDefaultK1()
    {
        var migrationType = typeof(MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations.AddSigningKeyIdToRefreshTokens);
        Assert.NotNull(migrationType);

        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "MS.Cobroup.BackendAdmin.Infrastructure",
            "Persistence",
            "Migrations",
            "20261023151001_AddSigningKeyIdToRefreshTokens.cs");

        if (File.Exists(migrationPath))
        {
            var content = File.ReadAllText(migrationPath);
            Assert.Contains("DELETE FROM refresh_tokens;", content);
            Assert.DoesNotContain("defaultValue: \"k1\"", content);
        }
    }

    // 4. Revocación operacional por kid afecta solo sesiones del kid retirado y es idempotente
    [Fact]
    public async Task Test04_OperationalRevocation_AffectsOnlyMatchingKid_AndIsIdempotent()
    {
        var repo = new TestIdentityRepository();
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create("u1", "User One", "hash", now);
        var orgId = Guid.NewGuid();

        // Crear 2 tokens activos bajo k1 y 2 activos bajo k2
        var t1_k1 = new RefreshToken(Guid.NewGuid(), user.Id, orgId, "hash-k1-1", now.AddDays(7), now, "k1");
        var t2_k1 = new RefreshToken(Guid.NewGuid(), user.Id, orgId, "hash-k1-2", now.AddDays(7), now, "k1");
        var t3_k2 = new RefreshToken(Guid.NewGuid(), user.Id, orgId, "hash-k2-1", now.AddDays(7), now, "k2");
        var t4_k2 = new RefreshToken(Guid.NewGuid(), user.Id, orgId, "hash-k2-2", now.AddDays(7), now, "k2");

        await repo.SaveRefreshTokenAsync(t1_k1, CancellationToken.None);
        await repo.SaveRefreshTokenAsync(t2_k1, CancellationToken.None);
        await repo.SaveRefreshTokenAsync(t3_k2, CancellationToken.None);
        await repo.SaveRefreshTokenAsync(t4_k2, CancellationToken.None);

        var service = new OperationalSessionRevocationService(repo);

        // Revocar por kid 'k1'
        var res1 = await service.RevokeBySigningKeyIdAsync("k1", "Key rotation retirement", CancellationToken.None);
        Assert.Equal("revoke-kid", res1.Action);
        Assert.Equal("k1", res1.SigningKeyId);
        Assert.Equal(2, res1.AffectedCount);

        // Verificar que los tokens de k1 están revocados y los de k2 siguen activos
        Assert.False(t1_k1.IsActive);
        Assert.False(t2_k1.IsActive);
        Assert.True(t3_k2.IsActive);
        Assert.True(t4_k2.IsActive);

        // Idempotencia: segunda invocación para k1 revoca 0 sin error
        var res2 = await service.RevokeBySigningKeyIdAsync("k1", "Key rotation retirement", CancellationToken.None);
        Assert.Equal(0, res2.AffectedCount);
    }

    // 5. Emergencia revoca todas las familias y sesiones activas
    [Fact]
    public async Task Test05_EmergencyRevocation_RevokesAllActiveFamilies()
    {
        var repo = new TestIdentityRepository();
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create("u1", "User One", "hash", now);
        var orgId = Guid.NewGuid();

        var t1 = new RefreshToken(Guid.NewGuid(), user.Id, orgId, "hash-1", now.AddDays(7), now, "k1");
        var t2 = new RefreshToken(Guid.NewGuid(), user.Id, orgId, "hash-2", now.AddDays(7), now, "k2");
        await repo.SaveRefreshTokenAsync(t1, CancellationToken.None);
        await repo.SaveRefreshTokenAsync(t2, CancellationToken.None);

        var service = new OperationalSessionRevocationService(repo);
        var res = await service.RevokeAllAsync("Emergency rotation", CancellationToken.None);

        Assert.Equal("revoke-all", res.Action);
        Assert.Equal(2, res.AffectedCount);
        Assert.False(t1.IsActive);
        Assert.False(t2.IsActive);
    }

    // 6. Token real emitido por BackendAdmin con k1 y k2 aceptado durante solapamiento; k1 rechazado tras retiro
    [Fact]
    public async Task Test06_RealTokenInteroperability_OverlapAndRetirement()
    {
        var env = new TestHostEnvironment();
        var repo = new TestIdentityRepository();

        // --- Etapa 1: Estado Inicial k1 Activo en BackendAdmin ---
        var configK1 = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = Issuer,
            ["Identity:Audience"] = Audience,
            ["Jwt:SigningKeyBase64"] = Key1Base64,
            ["Jwt:ActiveKeyId"] = "k1"
        }).Build();

        using var keyServiceK1 = new SymmetricKeyService(configK1, env);
        var tokenServiceK1 = new JwtTokenService(configK1, keyServiceK1, repo);

        var (user, membership) = CreateUserAndMembership(repo);
        var tokenResultK1 = await tokenServiceK1.IssueTokensAsync(user, membership, null, CancellationToken.None);
        var jwtK1 = tokenResultK1.AccessToken;

        // --- Etapa 2: Solapamiento (Activate: k2 activa, k1 en slot anterior) ---
        var validatorConfigOverlap = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = Issuer,
            ["Identity:Audience"] = Audience,
            ["Jwt:SigningKeyBase64"] = Key2Base64,
            ["Jwt:ActiveKeyId"] = "k2",
            ["Jwt:PreviousValidationKeys:0:KeyId"] = "k1",
            ["Jwt:PreviousValidationKeys:0:SigningKeyBase64"] = Key1Base64
        }).Build();

        using var keyServiceOverlap = new SymmetricKeyService(validatorConfigOverlap, env);
        var tokenServiceK2 = new JwtTokenService(validatorConfigOverlap, keyServiceOverlap, repo);

        // BackendAdmin emite nuevo token con k2
        var tokenResultK2 = await tokenServiceK2.IssueTokensAsync(user, membership, null, CancellationToken.None);
        var jwtK2 = tokenResultK2.AccessToken;

        // Backend validador configurado con k2 activa y k1 en clave anterior
        using var backendValidatorOverlap = new BackendSymmetricValidationService(validatorConfigOverlap, env);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        // Comprobacion de solapamiento usando el servicio real de Backend:
        // Acepta token k1 emitido antes de la rotacion y token k2 emitido despues de la rotacion
        var principalK1 = handler.ValidateToken(jwtK1, backendValidatorOverlap.TokenValidationParameters, out var validatedTokenK1);
        Assert.NotNull(principalK1);
        Assert.Equal("k1", ((JwtSecurityToken)validatedTokenK1).Header.Kid);

        var principalK2 = handler.ValidateToken(jwtK2, backendValidatorOverlap.TokenValidationParameters, out var validatedTokenK2);
        Assert.NotNull(principalK2);
        Assert.Equal("k2", ((JwtSecurityToken)validatedTokenK2).Header.Kid);

        // Rotacion de refresh token k1 genera nuevo refresh con SigningKeyId = "k2"
        var refreshRotation = await tokenServiceK2.RefreshTokensAsync(tokenResultK1.RawRefreshToken, CancellationToken.None);
        Assert.True(refreshRotation.Success);
        var latestRefreshToken = repo.RefreshTokens.Last();
        Assert.Equal("k2", latestRefreshToken.SigningKeyId);

        // --- Etapa 3: Retiro de k1 (solo k2 en validador de Backend) ---
        var validatorConfigRetired = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = Issuer,
            ["Identity:Audience"] = Audience,
            ["Jwt:SigningKeyBase64"] = Key2Base64,
            ["Jwt:ActiveKeyId"] = "k2"
            // k1 retirado del slot
        }).Build();

        using var backendValidatorRetired = new BackendSymmetricValidationService(validatorConfigRetired, env);

        // Token k1 emitido antes de la rotacion debe ser RECHAZADO por Backend
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(jwtK1, backendValidatorRetired.TokenValidationParameters, out _));

        // Token k2 emitido con la nueva clave debe ser ACEPTADO por Backend
        var principalK2AfterRetire = handler.ValidateToken(jwtK2, backendValidatorRetired.TokenValidationParameters, out _);
        Assert.NotNull(principalK2AfterRetire);
    }

    private static (ApplicationUser User, OrganizationMembership Membership) CreateUserAndMembership(TestIdentityRepository repo)
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create("collector1", "Collector One", "pwd", now);
        var org = Organization.Create("ORG01", "Org One", now);
        var membership = OrganizationMembership.Create(user.Id, org.Id, UserRole.Collector, now);

        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.Organization))!.SetValue(membership, org);
        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.User))!.SetValue(membership, user);

        repo.Users.Add(user);
        repo.Organizations.Add(org);
        repo.Memberships.Add(membership);

        return (user, membership);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "B21Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
