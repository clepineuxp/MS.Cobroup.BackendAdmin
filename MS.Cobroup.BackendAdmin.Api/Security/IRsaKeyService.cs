using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.BackendAdmin.Api.Security;

/// <summary>
/// Interfaz para servicios de firma RSA en BackendAdmin.
/// Extiende el puerto abstracto IJwtSigningKeyProvider para interoperabilidad y compatibilidad retroactiva.
/// </summary>
public interface IRsaKeyService : IJwtSigningKeyProvider
{
    string KeyId => ActiveKeyId;
    SigningCredentials SigningCredentials => GetSigningCredentials();
    TokenValidationParameters TokenValidationParameters => CreateTokenValidationParameters(string.Empty, string.Empty);
}
