using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAntiforgery antiforgery) : ControllerBase
{
    private const string RefreshCookieName = "__Host-cobroup-refresh";

    [HttpGet("antiforgery")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<AntiforgeryResponse> GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Headers.CacheControl = "no-store";
        return Ok(new AntiforgeryResponse(tokens.RequestToken ?? string.Empty));
    }


    [HttpPost("login")]
    [EnableRateLimiting("identity-write")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] AuthenticateUserService authentication,
        [FromServices] ITokenService tokenService,
        [FromServices] IIdentityRepository identityRepository,
        CancellationToken cancellationToken)
    {
        var result = await authentication.ExecuteAsync(
            request.Username,
            request.Password,
            cancellationToken);

        if (result.Outcome is AuthenticationOutcome.InvalidCredentials)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        if (result.Outcome is AuthenticationOutcome.MembershipRequired || result.Membership is null)
        {
            return Ok(new
            {
                status = "membership_required",
                userId = result.User!.Id
            });
        }

        var membership = result.Membership;
        if (request.OrganizationId.HasValue)
        {
            var specific = await identityRepository.SelectMembershipAsync(
                result.User!.Id,
                request.OrganizationId.Value,
                DateTimeOffset.UtcNow,
                cancellationToken);

            if (specific is null || !specific.IsActive)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "La organización solicitada no corresponde a una membresía activa del usuario."
                });
            }

            membership = specific;
        }

        var tokens = await tokenService.IssueTokensAsync(result.User!, membership, null, cancellationToken);

        SetRefreshTokenCookie(tokens.RawRefreshToken);

        return Ok(new
        {
            status = "authenticated",
            access_token = tokens.AccessToken,
            expires_in = tokens.ExpiresIn,
            token_type = tokens.TokenType,
            user = new
            {
                id = result.User!.Id,
                username = result.User.Username,
                displayName = result.User.DisplayName
            },
            organization = new
            {
                id = membership.OrganizationId,
                code = membership.Organization?.Code,
                name = membership.Organization?.Name,
                role = membership.Role.ToString()
            }
        });
    }

    [HttpPost("refresh")]
    [ValidateCobroupAntiForgeryToken]
    [EnableRateLimiting("identity-write")]
    public async Task<IActionResult> Refresh(
        [FromServices] ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        var rawRefreshToken = GetRefreshTokenFromCookie();
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Unauthorized(new { message = "Cookie de token de refresco ausente o inválida." });
        }

        var result = await tokenService.RefreshTokensAsync(rawRefreshToken, cancellationToken);
        if (!result.Success)
        {
            ClearRefreshTokenCookie();
            return Unauthorized(new { message = result.Error ?? "Token de refresco inválido o expirado." });
        }

        SetRefreshTokenCookie(result.RawRefreshToken!);

        return Ok(new
        {
            access_token = result.AccessToken,
            expires_in = result.ExpiresIn,
            token_type = "Bearer"
        });
    }

    [HttpPost("logout")]
    [ValidateCobroupAntiForgeryToken]
    [EnableRateLimiting("identity-write")]
    public async Task<IActionResult> Logout(
        [FromServices] ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        var rawRefreshToken = GetRefreshTokenFromCookie();
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            await tokenService.RevokeFamilyByTokenAsync(rawRefreshToken, "Usuario cerró sesión", cancellationToken);
        }

        ClearRefreshTokenCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser() => Ok(new
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("user_id"),
        organizationId = User.FindFirstValue("organization_id"),
        organizationCode = User.FindFirstValue("organization_code"),
        username = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("preferred_username") ?? User.FindFirstValue("unique_name"),
        displayName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name"),
        role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role")
    });

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

    private void ClearRefreshTokenCookie()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };

        Response.Cookies.Delete(RefreshCookieName, cookieOptions);
    }

    private string? GetRefreshTokenFromCookie()
    {
        return Request.Cookies[RefreshCookieName];
    }
}

public sealed record AntiforgeryResponse(string RequestToken);

public sealed record LoginRequest(string Username, string Password, Guid? OrganizationId = null);
