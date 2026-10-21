using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Customers;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize(Policy = "tenant-admin")]
public sealed class CustomersController(CustomerAdministrationService customers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? routeId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        var result = await customers.SearchAsync(actor, ReadBearer(), routeId, search, page, pageSize, cancellationToken);
        return Proxy(result);
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> Get(Guid customerId, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await customers.GetAsync(actor, ReadBearer(), customerId, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(JsonElement request, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await customers.CreateAsync(actor, ReadBearer(), request.GetRawText(), cancellationToken));
    }

    [HttpPut("{customerId:guid}")]
    public async Task<IActionResult> Update(Guid customerId, JsonElement request, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        return Proxy(await customers.UpdateAsync(actor, ReadBearer(), customerId, request.GetRawText(), cancellationToken));
    }

    private string ReadBearer()
    {
        var authorization = Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : string.Empty;
    }

    private IActionResult Proxy(CustomerProxyResult result) =>
        result.Content is null
            ? StatusCode(result.StatusCode)
            : new ContentResult
            {
                StatusCode = result.StatusCode,
                Content = result.Content,
                ContentType = result.ContentType + "; charset=utf-8"
            };
}
