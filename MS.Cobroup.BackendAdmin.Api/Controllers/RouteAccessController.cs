using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Routes;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/route-access")]
[Authorize(Policy = "tenant-user")]
public sealed class RouteAccessController(ListAccessibleRoutesService routes) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        return Ok(await routes.ExecuteAsync(actor, cancellationToken));
    }
}
