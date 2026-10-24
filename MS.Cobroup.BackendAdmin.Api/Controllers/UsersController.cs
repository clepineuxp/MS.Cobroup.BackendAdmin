using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Users;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = "tenant-admin")]
public sealed class UsersController(ListAssignableUsersService users, UserManagementService management) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        var result = await management.ListAsync(actor, cancellationToken);
        return result.Outcome == UserManagementOutcome.Success ? Ok(result.Users) : Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationUserInput input, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        if (Request.Headers.TryGetValue("X-Cobroup-Expected-Organization", out var expected)
            && (!Guid.TryParse(expected, out var expectedId) || expectedId != actor.OrganizationId))
            return Conflict(new { code = "organization_changed", message = "La organización de la sesión cambió. Revisa la organización activa e intenta de nuevo.", traceId = HttpContext.TraceIdentifier });
        var result = await management.CreateAsync(actor, input, cancellationToken);
        return result.Outcome switch
        {
            UserManagementOutcome.Success => StatusCode(StatusCodes.Status201Created, result.User),
            UserManagementOutcome.Forbidden => Forbid(),
            UserManagementOutcome.Conflict => Conflict(new { code = "username_unavailable", message = result.Error, traceId = HttpContext.TraceIdentifier }),
            _ => BadRequest(new { code = "invalid_user", message = result.Error, traceId = HttpContext.TraceIdentifier })
        };
    }

    [HttpGet("assignable")]
    public async Task<IActionResult> ListAssignable(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await users.ExecuteAsync(actor, cancellationToken);
        return result.Outcome is ListAssignableUsersOutcome.Success
            ? Ok(result.Users)
            : Forbid();
    }
}
