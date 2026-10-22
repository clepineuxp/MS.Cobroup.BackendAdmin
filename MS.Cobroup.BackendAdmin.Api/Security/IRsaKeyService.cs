using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.BackendAdmin.Api.Security;

public interface IRsaKeyService
{
    string KeyId { get; }
    SigningCredentials SigningCredentials { get; }
    TokenValidationParameters TokenValidationParameters { get; }
    JsonWebKeySet GetPublicJwks();
}
