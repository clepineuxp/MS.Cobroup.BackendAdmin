using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Users;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = "tenant-admin")]
public sealed class UsersController(ListAssignableUsersService users) : ControllerBase
{
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
