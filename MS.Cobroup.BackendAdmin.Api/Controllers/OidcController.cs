using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Api.Security;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
public sealed class OidcController(IConfiguration configuration) : ControllerBase
{
    [AcceptVerbs("GET", "POST")]
    [Route("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request is unavailable.");
        var authentication = await HttpContext.AuthenticateAsync(AuthenticationSchemes.AuthorityCookie);
        var hasOrganization = authentication.Principal?.HasClaim(claim => claim.Type == "organization_id") is true;
        if (!authentication.Succeeded || authentication.Principal is null || !hasOrganization)
        {
            var loginUri = request.ClientId switch
            {
                "cobroup-admin-spa" => configuration["Identity:Clients:Admin:LoginUri"],
                "cobroup-operational-spa" => configuration["Identity:Clients:Operational:LoginUri"],
                _ => null
            };
            if (string.IsNullOrWhiteSpace(loginUri))
            {
                return Problem("The login URI for this client is not configured.", statusCode: 500);
            }

            return Redirect(loginUri);
        }

        var identity = new ClaimsIdentity(
            authentication.Principal.Claims,
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources("cobroup-api");

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(claim.Type switch
            {
                OpenIddictConstants.Claims.Name or
                OpenIddictConstants.Claims.PreferredUsername or
                OpenIddictConstants.Claims.Role or
                "organization_id" or
                "organization_code" =>
                    [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
                _ => [OpenIddictConstants.Destinations.AccessToken]
            });
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [AcceptVerbs("GET", "POST")]
    [Route("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthenticationSchemes.AuthorityCookie);
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/userinfo")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public IActionResult UserInfo() => Ok(new
    {
        sub = User.FindFirstValue(OpenIddictConstants.Claims.Subject),
        name = User.FindFirstValue(OpenIddictConstants.Claims.Name),
        preferred_username = User.FindFirstValue(OpenIddictConstants.Claims.PreferredUsername),
        role = User.FindFirstValue(OpenIddictConstants.Claims.Role),
        organization_id = User.FindFirstValue("organization_id"),
        organization_code = User.FindFirstValue("organization_code")
    });
}
