using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.RouteFinances;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/route-finances")]
[Authorize(Policy = "tenant-admin")]
public sealed class RouteFinancesController(RouteFinanceAdministrationService service) : ControllerBase
{
    [HttpGet("{routeId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid routeId, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await service.GetBalanceAsync(actor, ReadBearer(), routeId, cancellationToken));
    }

    [HttpGet("entries")]
    public async Task<IActionResult> ListEntries(
        [FromQuery] Guid routeId,
        [FromQuery] string section,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await service.ListEntriesAsync(actor, ReadBearer(), routeId, section, cancellationToken));
    }

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry(JsonElement request, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await service.CreateEntryAsync(actor, ReadBearer(), request.GetRawText(), cancellationToken));
    }

    [HttpPut("entries/{entryId:guid}")]
    public async Task<IActionResult> UpdateEntry(
        Guid entryId,
        JsonElement request,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await service.UpdateEntryAsync(actor, ReadBearer(), entryId, request.GetRawText(), cancellationToken));
    }

    private string ReadBearer()
    {
        var authorization = Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : string.Empty;
    }

    private IActionResult Proxy(RouteFinanceProxyResult result) =>
        result.Content is null
            ? StatusCode(result.StatusCode)
            : new ContentResult
            {
                StatusCode = result.StatusCode,
                Content = result.Content,
                ContentType = result.ContentType + "; charset=utf-8"
            };
}
