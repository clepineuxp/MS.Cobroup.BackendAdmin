using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Identity;
using OpenIddict.Abstractions;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/auth/organizations")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.AuthorityCookie)]
public sealed class OrganizationSessionsController(
    OrganizationSessionService organizations,
    IAntiforgery antiforgery) : ControllerBase
{
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
    public async Task<IActionResult> Select(
        SelectOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var membership = await organizations.SelectAsync(userId, request.OrganizationId, cancellationToken);
        if (membership is null)
        {
            return Forbid(AuthenticationSchemes.AuthorityCookie);
        }

        await HttpContext.SignInAsync(
            AuthenticationSchemes.AuthorityCookie,
            AuthorityPrincipalFactory.Create(membership.User, membership));
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(OpenIddictConstants.Claims.Subject), out userId);
}

public sealed record SelectOrganizationRequest(Guid OrganizationId);
