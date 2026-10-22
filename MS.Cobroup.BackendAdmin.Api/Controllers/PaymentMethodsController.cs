using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.PaymentMethods;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/payment-methods")]
public sealed class PaymentMethodsController(PaymentMethodService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var items = await service.ListAllForAdminAsync(actor, cancellationToken);
        return Ok(items);
    }

    [HttpGet("available")]
    [Authorize(Policy = "tenant-user")]
    public async Task<IActionResult> ListAvailable(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var items = await service.ListAvailableAsync(actor.OrganizationId, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.GetAsync(actor, id, cancellationToken);
        return MapResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentMethodInput input,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.CreateAsync(actor, input, cancellationToken);
        return MapResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePaymentMethodInput input,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.UpdateAsync(actor, id, input, cancellationToken);
        return MapResult(result);
    }

    [HttpPost("{id:guid}/toggle-status")]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> ToggleStatus(
        Guid id,
        [FromBody] ToggleStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.SetStatusAsync(actor, id, request.IsActive, cancellationToken);
        return MapResult(result);
    }

    private IActionResult MapResult(PaymentMethodResult result)
    {
        if (result.Success && result.Item is not null)
        {
            return Ok(result.Item);
        }

        return result.ErrorType switch
        {
            PaymentMethodErrorType.Forbidden => Forbid(),
            PaymentMethodErrorType.NotFound => NotFound(new { error = result.ErrorMessage }),
            PaymentMethodErrorType.ValidationError => BadRequest(new { error = result.ErrorMessage }),
            PaymentMethodErrorType.DuplicateName => Conflict(new { error = result.ErrorMessage }),
            PaymentMethodErrorType.Conflict => Conflict(new { error = result.ErrorMessage }),
            _ => StatusCode(500, new { error = result.ErrorMessage })
        };
    }
}

public sealed record ToggleStatusRequest(bool IsActive);
