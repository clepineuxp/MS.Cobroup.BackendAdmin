using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.LoanExceptions;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/loan-exceptions")]
[Authorize(Policy = "tenant-admin")]
public sealed class LoanExceptionsController(LoanExceptionAdministrationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? routeId,
        [FromQuery] Guid? customerId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        var result = await service.SearchAsync(actor, ReadBearer(), routeId, customerId, status, page, pageSize, cancellationToken);
        return Proxy(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await service.GetAsync(actor, ReadBearer(), id, cancellationToken));
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> ApplyDecision(Guid id, JsonElement request, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await service.ApplyDecisionAsync(actor, ReadBearer(), id, request.GetRawText(), cancellationToken));
    }

    private string ReadBearer()
    {
        var authorization = Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : string.Empty;
    }

    private IActionResult Proxy(LoanExceptionProxyResult result) =>
        result.Content is null
            ? StatusCode(result.StatusCode)
            : new ContentResult
            {
                StatusCode = result.StatusCode,
                Content = result.Content,
                ContentType = result.ContentType + "; charset=utf-8"
            };
}
