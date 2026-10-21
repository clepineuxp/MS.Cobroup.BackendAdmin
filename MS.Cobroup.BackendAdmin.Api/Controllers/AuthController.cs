using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("antiforgery")]
    public ActionResult<AntiforgeryResponse> GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryResponse(tokens.RequestToken ?? string.Empty));
    }

    [HttpPost("session")]
    [EnableRateLimiting("identity-write")]
    public async Task<IActionResult> CreateSession(
        LoginRequest request,
        AuthenticateUserService authentication,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        var result = await authentication.ExecuteAsync(
            request.Username,
            request.Password,
            cancellationToken);

        if (result.Outcome is AuthenticationOutcome.InvalidCredentials)
        {
            return Unauthorized();
        }

        await HttpContext.SignInAsync(
            AuthenticationSchemes.AuthorityCookie,
            AuthorityPrincipalFactory.Create(result.User!, result.Membership));

        return result.Outcome is AuthenticationOutcome.MembershipRequired
            ? Ok(new LoginResponse("membership_required"))
            : NoContent();
    }

    [HttpDelete("session")]
    public async Task<IActionResult> DeleteSession()
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        await HttpContext.SignOutAsync(AuthenticationSchemes.AuthorityCookie);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public IActionResult GetCurrentUser() => Ok(new
    {
        userId = User.FindFirstValue(OpenIddictConstants.Claims.Subject),
        organizationId = User.FindFirstValue("organization_id"),
        organizationCode = User.FindFirstValue("organization_code"),
        username = User.FindFirstValue(OpenIddictConstants.Claims.PreferredUsername),
        displayName = User.FindFirstValue(OpenIddictConstants.Claims.Name),
        role = User.FindFirstValue(OpenIddictConstants.Claims.Role)
    });
}

public sealed record AntiforgeryResponse(string RequestToken);

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Status);
