using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Agenda;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize(Policy = "tenant-user")]
public sealed class AnalyticsController(AnalyticsAdministrationService service) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] Guid? routeId,
        [FromQuery] DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.GetOverviewAsync(actor, ReadBearer(), routeId, targetDate, cancellationToken);
        return Proxy(result);
    }

    [HttpGet("routes-performance")]
    public async Task<IActionResult> GetRoutesPerformance(
        [FromQuery] DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.GetRoutesPerformanceAsync(actor, ReadBearer(), targetDate, cancellationToken);
        return Proxy(result);
    }

    [HttpGet("aging")]
    public async Task<IActionResult> GetAging(
        [FromQuery] Guid? routeId,
        [FromQuery] DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.GetAgingReportAsync(actor, ReadBearer(), routeId, targetDate, cancellationToken);
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
