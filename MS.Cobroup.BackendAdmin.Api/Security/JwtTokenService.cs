using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

namespace MS.Cobroup.BackendAdmin.Api.Security;

public sealed class JwtTokenService(
    IConfiguration configuration,
    ISymmetricKeyService keyService,
    IIdentityRepository identityRepository) : ITokenService
{
    private const int AccessTokenLifetimeSeconds = 900; // 15 minutes
    private const int RefreshTokenLifetimeDays = 30; // 30 days
    private const string AudienceAccess = "cobroup-api";
    private const string AudienceRefresh = "cobroup-refresh";

    public async Task<TokenIssueResult> IssueTokensAsync(
        ApplicationUser user,
        OrganizationMembership membership,
        Guid? familyId,
        CancellationToken cancellationToken)
    {
        var actualFamilyId = familyId ?? Guid.NewGuid();
        var issuer = configuration["Identity:Issuer"]
            ?? throw new InvalidOperationException("Identity:Issuer is required.");

        var (accessToken, _) = GenerateAccessToken(user, membership, issuer);
        var (refreshTokenString, refreshTokenHash) = GenerateRefreshToken(user, membership, actualFamilyId, issuer);

        var now = DateTimeOffset.UtcNow;
        var refreshToken = new RefreshToken(
            actualFamilyId,
            user.Id,
            membership.OrganizationId,
            refreshTokenHash,
            now.AddDays(RefreshTokenLifetimeDays),
            now);

        await identityRepository.SaveRefreshTokenAsync(refreshToken, cancellationToken);

        return new TokenIssueResult(
            accessToken,
            refreshTokenString,
            AccessTokenLifetimeSeconds,
            actualFamilyId);
    }

    public async Task<TokenRefreshResult> RefreshTokensAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return new TokenRefreshResult(false, Error: "El token de refresco es requerido.");
        }

        var issuer = configuration["Identity:Issuer"]
            ?? throw new InvalidOperationException("Identity:Issuer is required.");

        // Validate the refresh token JWT cryptographically
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        JwtSecurityToken jwtToken;
        try
        {
            var refreshValidationParams = keyService.CreateTokenValidationParameters(issuer, AudienceRefresh);

            handler.ValidateToken(rawRefreshToken, refreshValidationParams, out var validatedToken);
            if (validatedToken is not JwtSecurityToken parsed ||
                !parsed.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new TokenRefreshResult(false, Error: "Algoritmo de token inválido.");
            }

            jwtToken = parsed;
            var typ = jwtToken.Claims.FirstOrDefault(c => c.Type == "typ")?.Value;
            if (typ != "refresh")
            {
                return new TokenRefreshResult(false, Error: "Tipo de token inválido para refresco.");
            }
        }
        catch (SecurityTokenException)
        {
            return new TokenRefreshResult(false, Error: "El token de refresco es inválido o ha expirado.");
        }

        var familyIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "family_id")?.Value;
        if (!Guid.TryParse(familyIdClaim, out var familyId))
        {
            return new TokenRefreshResult(false, Error: "El token de refresco carece de identificador de familia válido.");
        }

        var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return new TokenRefreshResult(false, Error: "El token de refresco carece de identificador de sujeto válido.");
        }

        var orgIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "organization_id")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var organizationId))
        {
            return new TokenRefreshResult(false, Error: "El token de refresco carece de identificador de organización válido.");
        }

        var currentTokenHash = HashToken(rawRefreshToken);
        var now = DateTimeOffset.UtcNow;

        // Revalidate user, organization and active membership
        var membership = await identityRepository.SelectMembershipAsync(
            userId,
            organizationId,
            now,
            cancellationToken);

        if (membership is null || membership.User is null || !membership.IsActive)
        {
            await identityRepository.RevokeFamilyAsync(familyId, now, "Usuario u organización inactiva durante refresco", cancellationToken);
            return new TokenRefreshResult(false, Error: "El usuario o membresía ya no se encuentran activos.");
        }

        // Generate new refresh token under the same family
        var (newRefreshTokenString, newRefreshTokenHash) = GenerateRefreshToken(membership.User, membership, familyId, issuer);
        var newRefreshToken = new RefreshToken(
            familyId,
            userId,
            organizationId,
            newRefreshTokenHash,
            now.AddDays(RefreshTokenLifetimeDays),
            now);

        // Atomic rotation in PostgreSQL
        var rotated = await identityRepository.RotateRefreshTokenAtomicAsync(
            currentTokenHash,
            newRefreshToken,
            now,
            cancellationToken);

        if (!rotated)
        {
            return new TokenRefreshResult(false, Error: "El token de refresco ya no es válido o fue reutilizado. La familia de sesiones ha sido revocada.");
        }

        var (accessToken, _) = GenerateAccessToken(membership.User, membership, issuer);

        return new TokenRefreshResult(
            true,
            accessToken,
            newRefreshTokenString,
            AccessTokenLifetimeSeconds);
    }

    public async Task<RevokeFamilyResult> RevokeFamilyByTokenAsync(
        string rawRefreshToken,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return new RevokeFamilyResult(false, Error: "El token de refresco no puede ser nulo o vacío.");
        }

        var hash = HashToken(rawRefreshToken);
        var existing = await identityRepository.FindRefreshTokenByHashAsync(hash, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            await identityRepository.RevokeFamilyAsync(existing.FamilyId, now, reason, cancellationToken);
            return new RevokeFamilyResult(true, existing.FamilyId);
        }

        // I-17: Never trust claims from unvalidated tokens.
        // Only extract family_id if the token passes full cryptographic validation.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var issuer = configuration["Identity:Issuer"]
                ?? throw new InvalidOperationException("Identity:Issuer is required.");
            var refreshValidationParams = keyService.CreateTokenValidationParameters(issuer, AudienceRefresh);

            handler.ValidateToken(rawRefreshToken, refreshValidationParams, out var validatedToken);
            if (validatedToken is JwtSecurityToken jwtToken &&
                string.Equals(jwtToken.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                var typ = jwtToken.Claims.FirstOrDefault(c => c.Type == "typ")?.Value;
                if (string.Equals(typ, "refresh", StringComparison.Ordinal))
                {
                    var familyClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "family_id")?.Value;
                    if (Guid.TryParse(familyClaim, out var familyId))
                    {
                        await identityRepository.RevokeFamilyAsync(familyId, now, reason, cancellationToken);
                        return new RevokeFamilyResult(true, familyId);
                    }
                }
            }
            return new RevokeFamilyResult(false, Error: "El token de refresco no contiene una estructura válida de familia.");
        }
        catch (SecurityTokenException ex)
        {
            return new RevokeFamilyResult(false, Error: $"Validación criptográfica fallida: {ex.Message}");
        }
    }

    public async Task<OrganizationSwitchResult> SwitchOrganizationSessionAsync(
        Guid expectedUserId,
        Guid expectedCurrentOrgId,
        string rawRefreshToken,
        Guid targetOrgId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return new OrganizationSwitchResult(false, StatusCode: 401, Error: "Cookie de refresco ausente o vacía.");
        }

        var now = DateTimeOffset.UtcNow;
        var issuer = configuration["Identity:Issuer"]
            ?? throw new InvalidOperationException("Identity:Issuer is required.");

        // 1. Cryptographic validation of incoming refresh token (RS256, audience, typ=refresh, valid signature and kid)
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        JwtSecurityToken jwtToken;
        try
        {
            var refreshValidationParams = keyService.CreateTokenValidationParameters(issuer, AudienceRefresh);

            handler.ValidateToken(rawRefreshToken, refreshValidationParams, out var validatedToken);
            if (validatedToken is not JwtSecurityToken parsed ||
                !string.Equals(parsed.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new OrganizationSwitchResult(false, StatusCode: 401, Error: "El token de refresco no utiliza el algoritmo HS256 requerido.");
            }
            jwtToken = parsed;
        }
        catch (SecurityTokenException)
        {
            return new OrganizationSwitchResult(false, StatusCode: 401, Error: "Firma, emisor, audiencia o vigencia del token de refresco inválida.");
        }

        var typ = jwtToken.Claims.FirstOrDefault(c => c.Type == "typ")?.Value;
        if (!string.Equals(typ, "refresh", StringComparison.Ordinal))
        {
            return new OrganizationSwitchResult(false, StatusCode: 401, Error: "El tipo de token no es de refresco.");
        }

        // 2. Coincidence between sub of access and sub of refresh
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subClaim, out var refreshUserId) || refreshUserId != expectedUserId)
        {
            return new OrganizationSwitchResult(false, StatusCode: 403, Error: "El sujeto del token de refresco no coincide con el usuario autenticado.");
        }

        // 3. Coincidence between current tenant in access token and organization in refresh token
        var orgClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "organization_id")?.Value;
        if (!Guid.TryParse(orgClaim, out var refreshOrgId) || refreshOrgId != expectedCurrentOrgId)
        {
            return new OrganizationSwitchResult(false, StatusCode: 403, Error: "La organización del token de refresco no coincide con la organización activa de la sesión.");
        }

        // 4. Verify target membership and target organization active
        var targetMembership = await identityRepository.SelectMembershipAsync(expectedUserId, targetOrgId, now, cancellationToken);
        if (targetMembership is null || !targetMembership.IsActive || targetMembership.Organization is null || targetMembership.Organization.Status != OrganizationStatus.Active)
        {
            return new OrganizationSwitchResult(false, StatusCode: 403, Error: "Membresía u organización destino no autorizada o suspendida.");
        }

        // 5. Generate initial refresh token for the new family
        var newFamilyId = Guid.NewGuid();
        var (newRefreshTokenString, newRefreshTokenHash) = GenerateRefreshToken(targetMembership.User, targetMembership, newFamilyId, issuer);

        var newRefreshTokenRecord = new RefreshToken(
            newFamilyId,
            targetMembership.UserId,
            targetMembership.OrganizationId,
            newRefreshTokenHash,
            now.AddDays(RefreshTokenLifetimeDays),
            now);

        // 6. Atomic PostgreSQL transaction: verify active token hash, revoke previous family, and persist new initial token
        var currentTokenHash = HashToken(rawRefreshToken);
        var switched = await identityRepository.SwitchOrganizationSessionAtomicAsync(
            currentTokenHash,
            expectedUserId,
            expectedCurrentOrgId,
            newRefreshTokenRecord,
            now,
            $"Tenant switched to organization {targetMembership.OrganizationId}",
            cancellationToken);

        if (!switched)
        {
            return new OrganizationSwitchResult(false, StatusCode: 401, Error: "No fue posible verificar y revocar atómicamente la sesión anterior, o el token fue reutilizado.");
        }

        // 7. Generate access token only after DB commit confirmed
        var (accessToken, _) = GenerateAccessToken(targetMembership.User, targetMembership, issuer);

        return new OrganizationSwitchResult(
            true,
            accessToken,
            newRefreshTokenString,
            AccessTokenLifetimeSeconds,
            targetMembership,
            StatusCode: 200);
    }

    private (string AccessToken, string Jti) GenerateAccessToken(
        ApplicationUser user,
        OrganizationMembership membership,
        string issuer)
    {
        var now = DateTimeOffset.UtcNow;
        var jti = Guid.NewGuid().ToString("N");
        var organizationCode = membership.Organization?.Code;
        if (string.IsNullOrWhiteSpace(organizationCode))
        {
            throw new InvalidOperationException("No se puede emitir un access token sin una organización activa y válida asignada a la membresía.");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("typ", "access"),
            new("organization_id", membership.OrganizationId.ToString()),
            new("organization_code", organizationCode),
            new(ClaimTypes.Role, membership.Role.ToString()),
            new("role", membership.Role.ToString()),
            new("preferred_username", user.Username),
            new(ClaimTypes.Name, user.DisplayName),
            new("user_id", user.Id.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = now.UtcDateTime.AddSeconds(AccessTokenLifetimeSeconds),
            Issuer = issuer,
            Audience = AudienceAccess,
            SigningCredentials = keyService.GetSigningCredentials()
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return (handler.WriteToken(token), jti);
    }

    private (string RefreshTokenString, string RefreshTokenHash) GenerateRefreshToken(
        ApplicationUser user,
        OrganizationMembership membership,
        Guid familyId,
        string issuer)
    {
        var now = DateTimeOffset.UtcNow;
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("family_id", familyId.ToString()),
            new("organization_id", membership.OrganizationId.ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("typ", "refresh")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = now.UtcDateTime.AddDays(RefreshTokenLifetimeDays),
            Issuer = issuer,
            Audience = AudienceRefresh,
            SigningCredentials = keyService.GetSigningCredentials()
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        var tokenString = handler.WriteToken(token);
        var tokenHash = HashToken(tokenString);

        return (tokenString, tokenHash);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
