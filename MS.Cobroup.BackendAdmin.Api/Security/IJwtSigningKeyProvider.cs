using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.BackendAdmin.Api.Security;

/// <summary>
/// Puerto abstracto para el proveedor de material criptográfico de firma y validación JWT.
/// Desacopla la autoridad de emisión y validación de tokens de los detalles de custodia
/// y persistencia física de claves (efímero en memoria, PEM, PFX, AWS KMS o Kubernetes Secret).
/// </summary>
public interface IJwtSigningKeyProvider : IDisposable
{
    /// <summary>
    /// Identificador único y no ambiguo de la clave activa (kid) para el ciclo de vida actual.
    /// </summary>
    string ActiveKeyId { get; }

    /// <summary>
    /// Obtiene las credenciales criptográficas de firma (RS256) asociadas a la clave activa.
    /// </summary>
    SigningCredentials GetSigningCredentials();

    /// <summary>
    /// Obtiene la colección de claves de seguridad públicas para validación de tokens.
    /// </summary>
    IReadOnlyCollection<SecurityKey> GetValidationKeys();

    /// <summary>
    /// Crea los parámetros estándar de validación de tokens JWT vinculados a las claves públicas del proveedor.
    /// </summary>
    TokenValidationParameters CreateTokenValidationParameters(string issuer, string audience);

    /// <summary>
    /// Exporta el conjunto de claves públicas en formato JWKS (RFC 7517) sin parámetros privados.
    /// </summary>
    JsonWebKeySet GetPublicJwks();
}
