using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.BackendAdmin.Api.Security;

/// <summary>
/// Proveedor de material criptográfico RSA efímero generado en memoria para entornos de desarrollo y pruebas.
/// 
/// Características de seguridad:
/// 1. Clave RSA de 2048 bits generada aleatoriamente al instanciar el proceso.
/// 2. La clave privada reside exclusivamente en la memoria volátil del proceso; nunca se exporta a disco, base de datos ni variables.
/// 3. Identificador de clave (kid) dinámico y único por ciclo de vida; dos instancias nunca comparten el mismo kid.
/// 4. Exporta únicamente parámetros públicos (n, e) a través de JWKS sin exponer parámetros privados (d, p, q, dp, dq, qi).
/// 5. Implementa IDisposable para liberar los recursos nativos del objeto RSA al finalizar la aplicación.
/// 
/// LIMITACIÓN ARQUITECTÓNICA OBLIGATORIA:
/// Los tokens emitidos dejan de ser válidos al reiniciar el proceso. No es apto para múltiples réplicas concurrentes
/// ni para producción. La integración con proveedor persistente (PEM, PFX, AWS KMS) es backlog previo a producción.
/// </summary>
public sealed class EphemeralRsaSigningKeyProvider : IJwtSigningKeyProvider, IRsaKeyService
{
    private readonly RSA rsa;
    private readonly RsaSecurityKey securityKey;
    private readonly SigningCredentials signingCredentials;
    private readonly string activeKid;
    private bool disposed;

    public string ActiveKeyId => activeKid;
    public string KeyId => activeKid;
    public SigningCredentials SigningCredentials => signingCredentials;
    public TokenValidationParameters TokenValidationParameters => CreateTokenValidationParameters(string.Empty, string.Empty);

    public EphemeralRsaSigningKeyProvider()
    {
        // 1. Generación de clave RSA 2048 bits mediante API criptográfica segura de .NET
        rsa = RSA.Create(2048);

        // 2. Generación de identificador único (kid) no ambiguo por ciclo de vida del proceso
        activeKid = $"cobroup-ephemeral-{Guid.NewGuid():N}";

        // 3. Configuración de credenciales de firma RS256
        securityKey = new RsaSecurityKey(rsa)
        {
            KeyId = activeKid
        };

        signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
    }

    public SigningCredentials GetSigningCredentials()
    {
        ThrowIfDisposed();
        return signingCredentials;
    }

    public IReadOnlyCollection<SecurityKey> GetValidationKeys()
    {
        ThrowIfDisposed();
        return [securityKey];
    }

    public TokenValidationParameters CreateTokenValidationParameters(string issuer, string audience)
    {
        ThrowIfDisposed();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                if (string.IsNullOrWhiteSpace(kid))
                {
                    return [];
                }

                return string.Equals(kid, activeKid, StringComparison.Ordinal)
                    ? [securityKey]
                    : [];
            },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RoleClaimType = "role",
            NameClaimType = "preferred_username"
        };

        if (!string.IsNullOrWhiteSpace(issuer))
        {
            validationParameters.ValidateIssuer = true;
            validationParameters.ValidIssuer = issuer;
        }
        else
        {
            validationParameters.ValidateIssuer = false;
        }

        if (!string.IsNullOrWhiteSpace(audience))
        {
            validationParameters.ValidateAudience = true;
            validationParameters.ValidAudience = audience;
        }
        else
        {
            validationParameters.ValidateAudience = false;
        }

        return validationParameters;
    }

    public JsonWebKeySet GetPublicJwks()
    {
        ThrowIfDisposed();

        // Exportación estricta de parámetros públicos únicamente (RFC 7517)
        // includePrivateParameters = false garantiza que d, p, q, dp, dq, qi nunca se incluyan
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        var jwk = new JsonWebKey
        {
            Kty = JsonWebAlgorithmsKeyTypes.RSA,
            Use = "sig",
            Alg = SecurityAlgorithms.RsaSha256,
            Kid = activeKid,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent)
        };

        var jwks = new JsonWebKeySet();
        jwks.Keys.Add(jwk);
        return jwks;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        rsa.Dispose();
        disposed = true;
    }
}
