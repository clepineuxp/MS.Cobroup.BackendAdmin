using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

/// <summary>
/// Pruebas obligatorias de la decisión arquitectónica de proveedor RSA efímero (ADR-0002):
/// Demuestra que el sistema opera en Development sin claves PEM externas,
/// valida fail-fast fuera de desarrollo, verifica el aislamiento estricto de JWKS sin material privado,
/// y documenta formalmente la limitación de sesiones entre reinicios.
/// </summary>
public sealed class EphemeralRsaSigningKeyProviderTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "MS.Cobroup.BackendAdmin";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void Test1_BackendAdmin_StartsInDevelopmentWithoutPem_WhenEphemeralRsaAuthorized()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningProvider"] = "EphemeralRsa",
            ["Jwt:AllowEphemeralSigning"] = "true",
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api"
        }).Build();

        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        using var provider = JwtSigningKeyProviderFactory.Create(config, env);

        Assert.NotNull(provider);
        Assert.StartsWith("cobroup-ephemeral-", provider.ActiveKeyId);
        Assert.Equal(SecurityAlgorithms.RsaSha256, provider.GetSigningCredentials().Algorithm);
        Assert.NotEmpty(provider.GetValidationKeys());
    }

    [Fact]
    public void Test2_BackendAdmin_Fails_WhenEphemeralModeSelectedWithoutAcceptanceFlag()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningProvider"] = "EphemeralRsa",
            ["Jwt:AllowEphemeralSigning"] = "false", // No autorizado explícitamente
            ["Identity:Issuer"] = "https://identity.cobroup.internal"
        }).Build();

        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => JwtSigningKeyProviderFactory.Create(config, env));
        Assert.Contains("Jwt:AllowEphemeralSigning=true", ex.Message);
    }

    [Fact]
    public void Test3_BackendAdmin_FailsOutsideDevelopment_WhenEphemeralModeUsedWithoutExemption()
    {
        var prodEnvs = new[] { Environments.Production, Environments.Staging, "QA" };

        foreach (var envName in prodEnvs)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningProvider"] = "EphemeralRsa",
                ["Jwt:AllowEphemeralSigning"] = "true",
                ["Identity:Issuer"] = "https://identity.cobroup.internal"
            }).Build();

            var env = new TestHostEnvironment { EnvironmentName = envName };

            var ex = Assert.Throws<InvalidOperationException>(() => JwtSigningKeyProviderFactory.Create(config, env));
            Assert.Contains("estrictamente rechazado fuera de Development", ex.Message);
        }
    }

    [Fact]
    public async Task Test4_AccessAndRefreshJwt_AreSignedWithRs256_AndContainActiveKid()
    {
        using var provider = new EphemeralRsaSigningKeyProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api"
        }).Build();

        var repository = new TestIdentityRepository();
        var tokenService = new JwtTokenService(config, provider, repository);

        var (user, membership) = CreateUserAndMembership();
        var result = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        // 1. Verificar Access Token
        var accessJwt = handler.ReadJwtToken(result.AccessToken);
        Assert.Equal(SecurityAlgorithms.RsaSha256, accessJwt.Header.Alg);
        Assert.Equal(provider.ActiveKeyId, accessJwt.Header.Kid);
        Assert.Equal("access", accessJwt.Claims.First(c => c.Type == "typ").Value);

        // 2. Verificar Refresh Token
        var refreshJwt = handler.ReadJwtToken(result.RawRefreshToken);
        Assert.Equal(SecurityAlgorithms.RsaSha256, refreshJwt.Header.Alg);
        Assert.Equal(provider.ActiveKeyId, refreshJwt.Header.Kid);
        Assert.Equal("refresh", refreshJwt.Claims.First(c => c.Type == "typ").Value);
    }

    [Fact]
    public async Task Test5_Tokens_AreValidatedWithPublicKeyPublishedByJwks()
    {
        using var provider = new EphemeralRsaSigningKeyProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api"
        }).Build();

        var repository = new TestIdentityRepository();
        var tokenService = new JwtTokenService(config, provider, repository);

        var (user, membership) = CreateUserAndMembership();
        var result = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);

        // Obtener el JWKS público
        var jwks = provider.GetPublicJwks();
        var jwk = Assert.Single(jwks.Keys);
        Assert.Equal(provider.ActiveKeyId, jwk.Kid);

        // Reconstruir clave de validación exclusivamente a partir de los parámetros públicos del JWKS
        var rsaParameters = new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(jwk.N),
            Exponent = Base64UrlEncoder.DecodeBytes(jwk.E)
        };
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsaParameters);
        var validationKey = new RsaSecurityKey(publicRsa) { KeyId = jwk.Kid };

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://identity.cobroup.internal",
            ValidateAudience = true,
            ValidAudience = "cobroup-api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = validationKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(result.AccessToken, validationParams, out var validatedToken);

        Assert.NotNull(principal);
        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [Fact]
    public void Test6_Jwks_ExposesOnlyPublicParameters_NeverPrivateKeys()
    {
        using var provider = new EphemeralRsaSigningKeyProvider();
        var jwks = provider.GetPublicJwks();

        Assert.NotNull(jwks);
        var jwk = Assert.Single(jwks.Keys);

        // Parámetros públicos requeridos
        Assert.Equal("RSA", jwk.Kty);
        Assert.Equal("sig", jwk.Use);
        Assert.Equal(SecurityAlgorithms.RsaSha256, jwk.Alg);
        Assert.False(string.IsNullOrWhiteSpace(jwk.Kid));
        Assert.False(string.IsNullOrWhiteSpace(jwk.N));
        Assert.False(string.IsNullOrWhiteSpace(jwk.E));

        // Parámetros privados estrictamente ausentes (deben ser null)
        Assert.Null(jwk.D);
        Assert.Null(jwk.P);
        Assert.Null(jwk.Q);
        Assert.Null(jwk.DP);
        Assert.Null(jwk.DQ);
        Assert.Null(jwk.QI);
    }

    [Fact]
    public async Task Test7_TokenIssuanceAndJwks_ShareTheSameSingletonInstance()
    {
        // En contenedor DI, el proveedor se registra como singleton
        using var singletonProvider = new EphemeralRsaSigningKeyProvider();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api"
        }).Build();

        var repository = new TestIdentityRepository();
        var tokenService = new JwtTokenService(config, singletonProvider, repository);

        var (user, membership) = CreateUserAndMembership();
        var result = await tokenService.IssueTokensAsync(user, membership, null, CancellationToken.None);

        // El endpoint JWKS lee del mismo singleton
        var jwks = singletonProvider.GetPublicJwks();
        var jwk = Assert.Single(jwks.Keys);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.AccessToken);

        // Coincidencia estricta de kid entre el token emitido y el JWKS publicado
        Assert.Equal(jwt.Header.Kid, jwk.Kid);
        Assert.Equal(singletonProvider.ActiveKeyId, jwk.Kid);
    }

    [Fact]
    public void Test8_TwoNewInstances_GenerateDifferentKeysAndKids()
    {
        using var instance1 = new EphemeralRsaSigningKeyProvider();
        using var instance2 = new EphemeralRsaSigningKeyProvider();

        // 1. Identificadores de clave completamente distintos
        Assert.NotEqual(instance1.ActiveKeyId, instance2.ActiveKeyId);

        // 2. Parámetros criptográficos públicos distintos
        var jwk1 = Assert.Single(instance1.GetPublicJwks().Keys);
        var jwk2 = Assert.Single(instance2.GetPublicJwks().Keys);

        Assert.NotEqual(jwk1.N, jwk2.N);
    }

    [Fact]
    public async Task Test9_TokenIssuedByFirstInstance_FailsValidationWithSecondInstance_DocumentingLimitation()
    {
        using var instance1 = new EphemeralRsaSigningKeyProvider();
        using var instance2 = new EphemeralRsaSigningKeyProvider();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = "https://identity.cobroup.internal",
            ["Identity:Audience"] = "cobroup-api"
        }).Build();

        var repository = new TestIdentityRepository();
        var tokenService1 = new JwtTokenService(config, instance1, repository);

        var (user, membership) = CreateUserAndMembership();
        var result = await tokenService1.IssueTokensAsync(user, membership, null, CancellationToken.None);

        // Intentar validar el token emitido por instance1 contra instance2 (simulando reinicio o segunda réplica)
        var validationParamsInstance2 = instance2.CreateTokenValidationParameters("https://identity.cobroup.internal", "cobroup-api");
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        // El token debe ser rechazado porque instance2 no reconoce el kid ni la clave privada de instance1
        var ex = Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(result.AccessToken, validationParamsInstance2, out _));

        Assert.NotNull(ex);
        // Esta limitación arquitectónica está documentada normativamente:
        // Las instancias efímeras no comparten claves entre reinicios ni entre réplicas.
    }

    private static (ApplicationUser User, OrganizationMembership Membership) CreateUserAndMembership()
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create("collector1", "Collector One", "hashed_pwd", now);
        var org = Organization.Create("ORG01", "Organization One", now);
        var membership = OrganizationMembership.Create(user.Id, org.Id, UserRole.Collector, now);
        var prop = typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.Organization));
        prop!.SetValue(membership, org);
        var userProp = typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.User));
        userProp!.SetValue(membership, user);

        return (user, membership);
    }
}
