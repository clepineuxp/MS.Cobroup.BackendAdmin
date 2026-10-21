using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using OpenIddict.Abstractions;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/routes")]
[Authorize(Policy = "tenant-admin")]
public sealed class RoutesController(CreateRouteService routes) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateRouteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantActor(out var actor))
        {
            return Unauthorized();
        }

        CreateRouteResult result;
        try
        {
            result = await routes.ExecuteAsync(
                new CreateRouteCommand(
                    actor,
                    request.Name,
                    request.Description,
                    request.SupervisorUserId,
                    request.CollectorUserIds ?? []),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        return result.Outcome switch
        {
            CreateRouteOutcome.Created => Created($"/api/v1/routes/{result.RouteId}", new { id = result.RouteId }),
            CreateRouteOutcome.Invalid => BadRequest(new { message = result.Error }),
            CreateRouteOutcome.Conflict => Conflict(new { message = result.Error }),
            CreateRouteOutcome.Forbidden => Forbid(),
            _ => Problem("Unexpected route creation result.")
        };
    }

    private bool TryGetTenantActor(out TenantActor actor)
    {
        var userIdText = User.FindFirstValue(OpenIddictConstants.Claims.Subject);
        var organizationIdText = User.FindFirstValue("organization_id");
        var roleText = User.FindFirstValue(OpenIddictConstants.Claims.Role);
        if (Guid.TryParse(userIdText, out var userId)
            && Guid.TryParse(organizationIdText, out var organizationId)
            && Enum.TryParse<UserRole>(roleText, ignoreCase: false, out var role))
        {
            actor = new TenantActor(userId, organizationId, role);
            return true;
        }

        actor = null!;
        return false;
    }
}

public sealed record CreateRouteRequest(
    string Name,
    string? Description,
    Guid SupervisorUserId,
    IReadOnlyCollection<Guid>? CollectorUserIds);
