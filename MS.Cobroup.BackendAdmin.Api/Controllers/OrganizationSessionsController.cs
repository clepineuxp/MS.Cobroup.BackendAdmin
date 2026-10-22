using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/auth/organizations")]
[Authorize]
public sealed class OrganizationSessionsController(
    OrganizationSessionService organizations,
    ITokenService tokenService,
    ILogger<OrganizationSessionsController> logger) : ControllerBase
{
    private const string RefreshCookieName = "__Host-cobroup-refresh";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await organizations.ListAsync(userId, cancellationToken));
    }

    [HttpPut("current")]
    [ValidateCobroupAntiForgeryToken]
    [EnableRateLimiting("identity-write")]
    public async Task<IActionResult> Select(
        SelectOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!TryGetCurrentOrganizationId(out var currentOrgId))
        {
            logger.LogWarning("Cambio de organización rechazado: claim organization_id ausente en el access token para usuario {UserId}", userId);
            ExpireRefreshTokenCookie();
            return Unauthorized(new { message = "Sesión no contiene contexto de organización válido." });
        }

        // B-17: Cookie __Host-cobroup-refresh es estrictamente obligatoria
        var currentRefreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(currentRefreshToken))
        {
            logger.LogWarning("Cambio de organización rechazado: cookie __Host-cobroup-refresh ausente para usuario {UserId}", userId);
            ExpireRefreshTokenCookie();
            return Unauthorized(new { message = "Sesión inválida o expirada. Se requiere cookie de refresco." });
        }

        // B-17: Validación criptográfica, coincidencia de sub/tenant, y transacción atómica en PostgreSQL
        var switchResult = await tokenService.SwitchOrganizationSessionAsync(
            userId,
            currentOrgId,
            currentRefreshToken,
            request.OrganizationId,
            cancellationToken);

        if (!switchResult.Success)
        {
            logger.LogWarning(
                "Cambio de organización fallido para usuario {UserId} hacia organización {TargetOrgId}. Motivo: {Reason}",
                userId,
                request.OrganizationId,
                switchResult.Error);

            ExpireRefreshTokenCookie();

            return StatusCode(switchResult.StatusCode, new
            {
                message = "Cambio de organización rechazado por seguridad."
            });
        }

        // Commit confirmado: establecer cookie con la nueva familia
        SetRefreshTokenCookie(switchResult.RawRefreshToken!);

        var membership = switchResult.TargetMembership!;
        logger.LogInformation(
            "Cambio de organización exitoso: usuario {UserId} cambió de organización {OldOrgId} a {NewOrgId}",
            userId,
            currentOrgId,
            membership.OrganizationId);

        return Ok(new
        {
            status = "authenticated",
            access_token = switchResult.AccessToken,
            expires_in = switchResult.ExpiresIn,
            token_type = "Bearer",
            organization = new
            {
                id = membership.OrganizationId,
                code = membership.Organization?.Code,
                name = membership.Organization?.Name,
                role = membership.Role.ToString()
            }
        });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        };

        Response.Cookies.Append(RefreshCookieName, refreshToken, cookieOptions);
    }

    private void ExpireRefreshTokenCookie()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };

        Response.Cookies.Append(RefreshCookieName, string.Empty, cookieOptions);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub") 
            ?? User.FindFirstValue("user_id");
        return Guid.TryParse(idClaim, out userId);
    }

    private bool TryGetCurrentOrganizationId(out Guid organizationId)
    {
        var orgClaim = User.FindFirstValue("organization_id")
            ?? User.FindFirstValue("org_id");
        return Guid.TryParse(orgClaim, out organizationId);
    }
}

public sealed record SelectOrganizationRequest(Guid OrganizationId);
