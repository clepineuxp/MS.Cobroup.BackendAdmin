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
    ITokenService tokenService) : ControllerBase
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

        var membership = await organizations.SelectAsync(userId, request.OrganizationId, cancellationToken);
        if (membership is null || !membership.IsActive)
        {
            return Forbid();
        }

        // B-05: Revoke previous family before issuing tokens for the new tenant
        var currentRefreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(currentRefreshToken))
        {
            await tokenService.RevokeFamilyByTokenAsync(
                currentRefreshToken,
                $"Tenant switched to organization {membership.OrganizationId}",
                cancellationToken);
        }

        // Issue new tokens under a new family
        var newFamilyId = Guid.NewGuid();
        var tokens = await tokenService.IssueTokensAsync(membership.User, membership, newFamilyId, cancellationToken);

        SetRefreshTokenCookie(tokens.RawRefreshToken);

        return Ok(new
        {
            status = "authenticated",
            access_token = tokens.AccessToken,
            expires_in = tokens.ExpiresIn,
            token_type = tokens.TokenType,
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

    private bool TryGetUserId(out Guid userId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub") 
            ?? User.FindFirstValue("user_id");
        return Guid.TryParse(idClaim, out userId);
    }
}

public sealed record SelectOrganizationRequest(Guid OrganizationId);
