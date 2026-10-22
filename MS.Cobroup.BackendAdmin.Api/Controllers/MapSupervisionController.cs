using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Agenda;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/map")]
[Authorize(Policy = "tenant-user")]
public sealed class MapSupervisionController(MapSupervisionService service) : ControllerBase
{
    [HttpGet("route-locations")]
    public async Task<IActionResult> GetRouteLocations(
        [FromQuery] Guid? routeId,
        [FromQuery] DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.GetMapLocationsAsync(actor, ReadBearer(), routeId, targetDate, cancellationToken);
        return Proxy(result);
    }

    private string ReadBearer()
    {
        var authorization = Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : string.Empty;
    }

    private IActionResult Proxy(AgendaProxyResult result) =>
        result.Content is null
            ? StatusCode(result.StatusCode)
            : new ContentResult
            {
                StatusCode = result.StatusCode,
                Content = result.Content,
                ContentType = result.ContentType + "; charset=utf-8"
            };
}
