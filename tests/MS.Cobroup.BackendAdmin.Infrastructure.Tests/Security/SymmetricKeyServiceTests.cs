using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Api.Security;
using Xunit;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

/// <summary>
/// Pruebas unitarias e integradas para ISymmetricKeyService y SymmetricKeyService.
/// Valida fail-fast riguroso (ausencia, vacío, placeholder, Base64 inválido, < 32 bytes),
/// allowlist exclusiva de HS256, rotación activa/anterior por kid, consistencia multirréplica y rechazo de algoritmos no autorizados.
/// </summary>
public sealed class SymmetricKeyServiceTests
{
    private static readonly byte[] ValidKey32Bytes = RandomNumberGenerator.GetBytes(32);
    private static readonly string ValidKey32Base64 = Convert.ToBase64String(ValidKey32Bytes);

    private static readonly byte[] ValidKey64Bytes = RandomNumberGenerator.GetBytes(64);
    private static readonly string ValidKey64Base64 = Convert.ToBase64String(ValidKey64Bytes);

    private static readonly byte[] InvalidKey31Bytes = RandomNumberGenerator.GetBytes(31);
    private static readonly string InvalidKey31Base64 = Convert.ToBase64String(InvalidKey31Bytes);

    private static IConfiguration CreateConfig(
        string? signingKey = null,
        string? activeKid = "k1",
        string? issuer = "https://dev-auth.cobroup.com",
        string? audience = "cobroup-api",
        Dictionary<string, string>? previousKeys = null)
    {
        var dict = new Dictionary<string, string?>();

        if (signingKey is not null)
        {
            dict["Jwt:SigningKeyBase64"] = signingKey;
        }

        if (activeKid is not null)
        {
            dict["Jwt:ActiveKeyId"] = activeKid;
        }

        if (issuer is not null)
        {
            dict["Identity:Issuer"] = issuer;
        }

        if (audience is not null)
        {
            dict["Identity:Audience"] = audience;
        }

        if (previousKeys is not null)
        {
            var idx = 0;
            foreach (var kvp in previousKeys)
            {
                dict[$"Jwt:PreviousValidationKeys:{idx}:KeyId"] = kvp.Key;
                dict[$"Jwt:PreviousValidationKeys:{idx}:SigningKeyBase64"] = kvp.Value;
                idx++;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void Test1_BackendAdmin_Starts_WhenSymmetricKeyValid()
    {
        var config = CreateConfig(ValidKey32Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        using var service = new SymmetricKeyService(config, env);

        Assert.Equal("k1", service.ActiveKeyId);
        Assert.NotNull(service.GetSigningCredentials());
        Assert.Equal(SecurityAlgorithms.HmacSha256, service.GetSigningCredentials().Algorithm);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Test2_FailFast_WhenKeyMissingOrEmpty(string? emptyKey)
    {
        var config = CreateConfig(emptyKey, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(config, env));
        Assert.Contains("obligatoria", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CHANGE_ME")]
    [InlineData("CHANGE_ME_DEV_KEY")]
    [InlineData("${{Dev_Jwt__SigningKeyBase64}}")]
    [InlineData("PLACEHOLDER_KEY_HERE")]
    public void Test3_FailFast_WhenKeyContainsPlaceholder(string placeholder)
    {
        var config = CreateConfig(placeholder, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(config, env));
        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-a-valid-base64-string!")]
    [InlineData("====invalid====")]
    [InlineData("12345")]
    public void Test4_FailFast_WhenKeyInvalidBase64(string invalidBase64)
    {
        var config = CreateConfig(invalidBase64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(config, env));
        Assert.Contains("Base64 válida", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test5_FailFast_WhenKeyUnder32Bytes_31BytesRejected()
    {
        var config = CreateConfig(InvalidKey31Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(config, env));
        Assert.Contains("longitud mínima de 32 bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("31", ex.Message);
    }

    [Fact]
    public void Test6_Success_WhenKeyExactly32BytesAnd64Bytes()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        using var service32 = new SymmetricKeyService(CreateConfig(ValidKey32Base64), env);
        Assert.NotNull(service32.GetSigningCredentials());

        using var service64 = new SymmetricKeyService(CreateConfig(ValidKey64Base64), env);
        Assert.NotNull(service64.GetSigningCredentials());
    }

    [Fact]
    public void Test7_Tokens_AreIssuedWithHs256AndActiveKid()
    {
        var config = CreateConfig(ValidKey32Base64, "k-prod-2026");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        using var service = new SymmetricKeyService(config, env);

        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = service.GetSigningCredentials()
        };

        var token = handler.CreateToken(descriptor);
        var jwt = Assert.IsType<JwtSecurityToken>(token);

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.Equal("k-prod-2026", jwt.Header.Kid);
    }

    [Fact]
    public void Test8_Tokens_AreValidatedWithActiveKey()
    {
        var config = CreateConfig(ValidKey32Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        using var service = new SymmetricKeyService(config, env);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("typ", "access")
            ]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = service.GetSigningCredentials()
        };

        var rawToken = handler.WriteToken(handler.CreateToken(descriptor));

        var valParams = service.CreateTokenValidationParameters("https://dev-auth.cobroup.com", "cobroup-api");
        var principal = handler.ValidateToken(rawToken, valParams, out var validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
    }

    [Fact]
    public void Test9_PreviousKeys_ValidateTokensDuringOverlapWindow()
    {
        var prevKeyBytes = RandomNumberGenerator.GetBytes(32);
        var prevKeyBase64 = Convert.ToBase64String(prevKeyBytes);

        var previousDict = new Dictionary<string, string>
        {
            ["k0"] = prevKeyBase64
        };

        // Servicio actual con clave activa k1 y clave anterior k0
        var config = CreateConfig(ValidKey32Base64, "k1", previousKeys: previousDict);
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        using var currentService = new SymmetricKeyService(config, env);

        // Emitir token simulando emisor previo con k0
        var prevKey = new SymmetricSecurityKey(prevKeyBytes) { KeyId = "k0" };
        var prevCreds = new SigningCredentials(prevKey, SecurityAlgorithms.HmacSha256);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = prevCreds
        };
        var tokenFromOldKey = handler.WriteToken(handler.CreateToken(descriptor));

        // El servicio actual debe validar exitosamente usando k0 de PreviousValidationKeys
        var valParams = currentService.CreateTokenValidationParameters("https://dev-auth.cobroup.com", "cobroup-api");
        var principal = handler.ValidateToken(tokenFromOldKey, valParams, out var validatedToken);

        Assert.NotNull(principal);
        Assert.Equal("k0", ((JwtSecurityToken)validatedToken).Header.Kid);
    }

    [Fact]
    public void Test10_RetiredKey_FailsValidation()
    {
        // Servicio donde k0 ya fue retirada (no está en PreviousValidationKeys)
        var config = CreateConfig(ValidKey32Base64, "k1", previousKeys: null);
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        using var currentService = new SymmetricKeyService(config, env);

        // Token emitido con una clave k0 no presente
        var retiredKeyBytes = RandomNumberGenerator.GetBytes(32);
        var retiredKey = new SymmetricSecurityKey(retiredKeyBytes) { KeyId = "k0" };
        var retiredCreds = new SigningCredentials(retiredKey, SecurityAlgorithms.HmacSha256);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = retiredCreds
        };
        var tokenFromRetiredKey = handler.WriteToken(handler.CreateToken(descriptor));

        var valParams = currentService.CreateTokenValidationParameters("https://dev-auth.cobroup.com", "cobroup-api");
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(tokenFromRetiredKey, valParams, out _));
    }

    [Fact]
    public void Test11_UnknownKid_FailsValidation()
    {
        var config = CreateConfig(ValidKey32Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        using var service = new SymmetricKeyService(config, env);

        // Token con kid desconocido pero firmado con la misma clave
        var keyWithUnknownKid = new SymmetricSecurityKey(ValidKey32Bytes) { KeyId = "unknown-kid" };
        var creds = new SigningCredentials(keyWithUnknownKid, SecurityAlgorithms.HmacSha256);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = creds
        };
        var token = handler.WriteToken(handler.CreateToken(descriptor));

        var valParams = service.CreateTokenValidationParameters("https://dev-auth.cobroup.com", "cobroup-api");
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(token, valParams, out _));
    }

    [Fact]
    public void Test12_AlgorithmAllowlist_RejectsNoneRs256Es256Hs384Hs512()
    {
        var config = CreateConfig(ValidKey32Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        using var service = new SymmetricKeyService(config, env);

        var valParams = service.CreateTokenValidationParameters("https://dev-auth.cobroup.com", "cobroup-api");

        // ValidAlgorithms debe contener ÚNICAMENTE HmacSha256
        Assert.Single(valParams.ValidAlgorithms);
        Assert.Contains(SecurityAlgorithms.HmacSha256, valParams.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.None, valParams.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.RsaSha256, valParams.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.EcdsaSha256, valParams.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.HmacSha384, valParams.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.HmacSha512, valParams.ValidAlgorithms);

        // Prueba activa de rechazo de token RSA
        using var rsa = RSA.Create(2048);
        var rsaCreds = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "k1" }, SecurityAlgorithms.RsaSha256);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = rsaCreds
        };
        var rsaToken = handler.WriteToken(handler.CreateToken(descriptor));

        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(rsaToken, valParams, out _));
    }

    [Fact]
    public void Test13_MultiReplicaConsistency_IdenticalKeysValidate()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Dos instancias independientes (réplicas A y B) con el mismo secreto simétrico
        using var replicaA = new SymmetricKeyService(CreateConfig(ValidKey32Base64, "k1"), env);
        using var replicaB = new SymmetricKeyService(CreateConfig(ValidKey32Base64, "k1"), env);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = replicaA.GetSigningCredentials()
        };
        var tokenFromA = handler.WriteToken(handler.CreateToken(descriptor));

        // Replica B valida el token emitido por Réplica A
        var valParamsB = replicaB.CreateTokenValidationParameters("https://dev-auth.cobroup.com", "cobroup-api");
        var principal = handler.ValidateToken(tokenFromA, valParamsB, out var validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
    }

    [Fact]
    public void Test14_DivergentKeys_RejectTokens()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var secretA = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var secretB = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        using var serviceA = new SymmetricKeyService(CreateConfig(secretA, "k1"), env);
        using var serviceB = new SymmetricKeyService(CreateConfig(secretB, "k1"), env);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = serviceA.GetSigningCredentials()
        };
        var tokenFromA = handler.WriteToken(handler.CreateToken(descriptor));

        // Service B (con secreto diferente) debe rechazar la firma
        var valParamsB = serviceB.CreateTokenValidationParameters("https://dev-auth.cobroup.com", "cobroup-api");
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(tokenFromA, valParamsB, out _));
    }

    [Fact]
    public void Test15_FailFast_InProduction_WhenIssuerNotHttpsOrAudienceInvalid()
    {
        var prodEnv = new TestHostEnvironment { EnvironmentName = Environments.Production };

        var httpIssuerConfig = CreateConfig(ValidKey32Base64, "k1", issuer: "http://insecure-issuer.com");
        var exHttp = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(httpIssuerConfig, prodEnv));
        Assert.Contains("HTTPS", exHttp.Message);

        var wrongAudienceConfig = CreateConfig(ValidKey32Base64, "k1", audience: "wrong-audience");
        var exAud = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(wrongAudienceConfig, prodEnv));
        Assert.Contains("cobroup-api", exAud.Message);
    }

    [Fact]
    public void Test16_KeySecret_NeverAppearsInExceptionMessages()
    {
        var secretCandidate = Convert.ToBase64String(RandomNumberGenerator.GetBytes(31));
        var config = CreateConfig(secretCandidate, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricKeyService(config, env));
        Assert.DoesNotContain(secretCandidate, ex.Message);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "MS.Cobroup.BackendAdmin";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
