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
    IRsaKeyService rsaKeyService,
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
            var refreshValidationParams = rsaKeyService.TokenValidationParameters.Clone();
            refreshValidationParams.ValidAudience = AudienceRefresh;

            handler.ValidateToken(rawRefreshToken, refreshValidationParams, out var validatedToken);
            if (validatedToken is not JwtSecurityToken parsed ||
                !parsed.Header.Alg.Equals(SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
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

    public async Task RevokeFamilyByTokenAsync(
        string rawRefreshToken,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        var hash = HashToken(rawRefreshToken);
        var existing = await identityRepository.FindRefreshTokenByHashAsync(hash, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            await identityRepository.RevokeFamilyAsync(existing.FamilyId, now, reason, cancellationToken);
            return;
        }

        // I-17: Never trust claims from unvalidated tokens.
        // Only extract family_id if the token passes full cryptographic validation.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var refreshValidationParams = rsaKeyService.TokenValidationParameters.Clone();
            refreshValidationParams.ValidAudience = AudienceRefresh;

            handler.ValidateToken(rawRefreshToken, refreshValidationParams, out var validatedToken);
            if (validatedToken is JwtSecurityToken jwtToken &&
                string.Equals(jwtToken.Header.Alg, SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
            {
                var typ = jwtToken.Claims.FirstOrDefault(c => c.Type == "typ")?.Value;
                if (string.Equals(typ, "refresh", StringComparison.Ordinal))
                {
                    var familyClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "family_id")?.Value;
                    if (Guid.TryParse(familyClaim, out var familyId))
                    {
                        await identityRepository.RevokeFamilyAsync(familyId, now, reason, cancellationToken);
                    }
                }
            }
        }
        catch (SecurityTokenException)
        {
            // Cryptographic validation failed; do not trust unverified claims.
        }
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
            organizationCode = "DEFAULT";
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
            SigningCredentials = rsaKeyService.SigningCredentials
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
            SigningCredentials = rsaKeyService.SigningCredentials
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
