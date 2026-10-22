using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.BackendAdmin.Api.Security;

public sealed class RsaKeyService : IRsaKeyService
{
    private readonly RSA activeRsa;
    private readonly RSA? previousRsa;
    private readonly string activeKid;
    private readonly string? previousKid;
    private readonly string issuer;
    private readonly string audience;

    public string KeyId => activeKid;
    public SigningCredentials SigningCredentials { get; }
    public TokenValidationParameters TokenValidationParameters { get; }

    public RsaKeyService(IConfiguration configuration, IHostEnvironment environment)
    {
        issuer = configuration["Identity:Issuer"]
            ?? throw new InvalidOperationException("Identity:Issuer is required.");
        audience = configuration["Identity:Audience"] ?? "cobroup-api";

        var configuredKid = configuration["Jwt:KeyId"] ?? configuration["Jwt:Kid"];
        var privateKeyPem = configuration["Jwt:PrivateKeyPem"]
            ?? configuration["Jwt:SigningKeyPem"];

        var privateKeyPath = configuration["Jwt:PrivateKeyPath"];
        if (string.IsNullOrWhiteSpace(privateKeyPem) && !string.IsNullOrWhiteSpace(privateKeyPath) && File.Exists(privateKeyPath))
        {
            privateKeyPem = File.ReadAllText(privateKeyPath);
        }

        if (!environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(privateKeyPem))
            {
                throw new InvalidOperationException("Jwt:PrivateKeyPem is required outside Development.");
            }

            if (string.IsNullOrWhiteSpace(configuredKid))
            {
                throw new InvalidOperationException("Jwt:KeyId is required outside Development.");
            }

            if (!issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Identity:Issuer must be a valid HTTPS URL outside Development. Current: '{issuer}'");
            }

            if (!string.Equals(audience, "cobroup-api", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Identity:Audience must be 'cobroup-api'. Current: '{audience}'");
            }
        }

        activeKid = !string.IsNullOrWhiteSpace(configuredKid) ? configuredKid : "cobroup-dev-key-1";

        if (!string.IsNullOrWhiteSpace(privateKeyPem))
        {
            activeRsa = RSA.Create();
            ImportPemKey(activeRsa, privateKeyPem);
        }
        else
        {
            activeRsa = RSA.Create(2048);
        }

        var activeKey = new RsaSecurityKey(activeRsa) { KeyId = activeKid };
        SigningCredentials = new SigningCredentials(activeKey, SecurityAlgorithms.RsaSha256);

        var validationKeys = new List<SecurityKey> { activeKey };

        // Support overlapping rotation with previous public key
        var previousKeyPem = configuration["Jwt:PreviousPublicKeyPem"];
        previousKid = configuration["Jwt:PreviousKeyId"];
        if (!string.IsNullOrWhiteSpace(previousKeyPem) && !string.IsNullOrWhiteSpace(previousKid))
        {
            previousRsa = RSA.Create();
            ImportPemKey(previousRsa, previousKeyPem);
            validationKeys.Add(new RsaSecurityKey(previousRsa) { KeyId = previousKid });
        }

        TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = validationKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };
    }

    public JsonWebKeySet GetPublicJwks()
    {
        var jwks = new JsonWebKeySet();

        jwks.Keys.Add(CreateJwk(activeRsa, activeKid));

        if (previousRsa is not null && !string.IsNullOrWhiteSpace(previousKid))
        {
            jwks.Keys.Add(CreateJwk(previousRsa, previousKid));
        }

        return jwks;
    }

    private static JsonWebKey CreateJwk(RSA rsa, string kid)
    {
        var parameters = rsa.ExportParameters(false);
        return new JsonWebKey
        {
            Kty = JsonWebAlgorithmsKeyTypes.RSA,
            Use = "sig",
            Alg = SecurityAlgorithms.RsaSha256,
            Kid = kid,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent)
        };
    }

    private static void ImportPemKey(RSA rsa, string pem)
    {
        var trimmed = pem.Trim();
        try
        {
            rsa.ImportFromPem(trimmed);
        }
        catch
        {
            // If raw base64 or PKCS8/PKCS1 without standard PEM headers
            var clean = trimmed
                .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                .Replace("-----END RSA PRIVATE KEY-----", "")
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();

            var bytes = Convert.FromBase64String(clean);
            try
            {
                rsa.ImportPkcs8PrivateKey(bytes, out _);
            }
            catch
            {
                try
                {
                    rsa.ImportRSAPrivateKey(bytes, out _);
                }
                catch
                {
                    rsa.ImportSubjectPublicKeyInfo(bytes, out _);
                }
            }
        }
    }
}
