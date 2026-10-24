using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.LoanTemplates;
using MS.Cobroup.BackendAdmin.Domain.LoanTemplates;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/loan-templates")]
public sealed class LoanTemplatesController(LoanTemplateService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.ListAsync(actor, false, cancellationToken);
        return result.Outcome is LoanTemplateOutcome.Success ? Ok(result.Templates) : Forbid();
    }

    [HttpGet("available")]
    [Authorize(Policy = "tenant-user")]
    public async Task<IActionResult> ListAvailable(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.ListAsync(actor, true, cancellationToken);
        return result.Outcome is LoanTemplateOutcome.Success ? Ok(result.Templates) : Forbid();
    }

    [HttpGet("{templateId:guid}")]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Get(Guid templateId, CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.GetAsync(actor, templateId, cancellationToken);
        return Map(result);
    }

    [HttpPost]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Create(
        UpsertLoanTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.CreateAsync(actor, ToInput(request), cancellationToken);
        return result.Outcome is LoanTemplateOutcome.Created
            ? Created($"/api/v1/loan-templates/{result.Template!.Id}", result.Template)
            : Map(result);
    }

    [HttpPut("{templateId:guid}")]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Update(
        Guid templateId,
        UpsertLoanTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        return Map(await service.UpdateAsync(actor, templateId, ToInput(request), cancellationToken));
    }

    private IActionResult Map(LoanTemplateResult result) => result.Outcome switch
    {
        LoanTemplateOutcome.Success => Ok(result.Template),
        LoanTemplateOutcome.Invalid => BadRequest(new { message = result.Error }),
        LoanTemplateOutcome.Forbidden => Forbid(),
        LoanTemplateOutcome.NotFound => NotFound(),
        LoanTemplateOutcome.Conflict => Conflict(new { message = result.Error }),
        _ => Problem("Unexpected loan template result.")
    };

    private static LoanTemplateInput ToInput(UpsertLoanTemplateRequest request) => new(
        request.Name,
        request.Description,
        request.Classification,
        request.Status,
        request.MaxLoanDays,
        request.MaxPrincipalAmount,
        request.AllowedPercentages, request.MinPrincipalAmount, request.MinPercentage, request.MaxPercentage, request.RouteIds);
}

public sealed record UpsertLoanTemplateRequest(
    string Name,
    string? Description,
    LoanTemplateClassification Classification,
    LoanTemplateStatus Status,
    int MaxLoanDays,
    decimal MaxPrincipalAmount,
    IReadOnlyCollection<decimal>? AllowedPercentages = null,
    decimal MinPrincipalAmount = 0.01m,
    decimal? MinPercentage = null,
    decimal? MaxPercentage = null,
    IReadOnlyCollection<Guid>? RouteIds = null);
