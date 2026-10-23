using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.BackendAdmin.Api.Security;

/// <summary>
/// Abstracción para la gestión de claves simétricas HS256 de BackendAdmin.
/// Administra la clave activa de emisión y la colección de claves anteriores para validación.
/// </summary>
public interface ISymmetricKeyService : IDisposable
{
    /// <summary>
    /// Identificador público no secreto de la clave de firma activa (kid).
    /// </summary>
    string ActiveKeyId { get; }

    /// <summary>
    /// Credenciales criptográficas simétricas (HS256) asociadas a la clave activa.
    /// </summary>
    SigningCredentials GetSigningCredentials();

    /// <summary>
    /// Claves simétricas disponibles exclusivamente para validación (activa + anteriores).
    /// </summary>
    IReadOnlyCollection<SecurityKey> GetValidationKeys();

    /// <summary>
    /// Parámetros estándar de validación de tokens con allowlist estricta para HS256.
    /// </summary>
    TokenValidationParameters CreateTokenValidationParameters(string issuer, string audience);

    /// <summary>
    /// Parámetros estándar de validación vinculados al emisor y audiencia configurados.
    /// </summary>
    TokenValidationParameters TokenValidationParameters { get; }
}
