using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Agenda;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Directories;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/non-payment-reasons")]
[Authorize(Policy = "tenant-admin")]
public sealed class NonPaymentReasonsController(IOperationalAgendaDirectory directory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        if (!TenantActorReader.TryRead(User, out _))
        {
            return Unauthorized();
        }

        var result = await directory.GetNonPaymentReasonsAsync(ReadBearer(), includeInactive, cancellationToken);
        return Proxy(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateNonPaymentReasonRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TenantActorReader.TryRead(User, out _))
        {
            return Unauthorized();
        }

        var result = await directory.CreateNonPaymentReasonAsync(
            ReadBearer(),
            request.Name,
            request.Description,
            request.DisplayOrder,
            cancellationToken);
        return Proxy(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateNonPaymentReasonRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TenantActorReader.TryRead(User, out _))
        {
            return Unauthorized();
        }

        var result = await directory.UpdateNonPaymentReasonAsync(
            ReadBearer(),
            id,
            request.Name,
            request.Description,
            request.DisplayOrder,
            request.IsActive,
            cancellationToken);
        return Proxy(result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ToggleStatus(
        Guid id,
        [FromBody] ToggleNonPaymentReasonStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TenantActorReader.TryRead(User, out _))
        {
            return Unauthorized();
        }

        var result = await directory.ToggleNonPaymentReasonStatusAsync(ReadBearer(), id, request.IsActive, cancellationToken);
        return Proxy(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TenantActorReader.TryRead(User, out _))
        {
            return Unauthorized();
        }

        var result = await directory.DeleteNonPaymentReasonAsync(ReadBearer(), id, cancellationToken);
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

public sealed record CreateNonPaymentReasonRequest(string Name, string? Description, int DisplayOrder = 1);

public sealed record UpdateNonPaymentReasonRequest(string Name, string? Description, int DisplayOrder, bool IsActive);

public sealed record ToggleNonPaymentReasonStatusRequest(bool IsActive);
