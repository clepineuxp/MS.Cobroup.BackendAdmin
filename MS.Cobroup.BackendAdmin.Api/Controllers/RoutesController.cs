using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.LoanTemplates;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/routes")]
[Authorize(Policy = "tenant-admin")]
public sealed class RoutesController(
    CreateRouteService createRoute,
    ListRoutesService listRoutes,
    UpdateRouteService updateRoute,
    LoanTemplateService loanTemplates) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await listRoutes.ExecuteAsync(actor, cancellationToken);
        return result.Outcome is ListRoutesOutcome.Success
            ? Ok(result.Routes)
            : Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateRouteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        if (!request.InitialBalance.HasValue)
        {
            return BadRequest(new { message = "El saldo inicial de la ruta es obligatorio." });
        }

        CreateRouteResult result;
        try
        {
            result = await createRoute.ExecuteAsync(
                new CreateRouteCommand(
                    actor,
                    request.Name,
                    request.Description,
                    request.SupervisorUserId,
                    request.CollectorUserIds ?? [],
                    request.MaxActiveCustomersMode,
                    request.MaxActiveCustomersOverride,
                    request.MaxActiveLoansMode,
                    request.MaxActiveLoansOverride,
                    request.InitialBalance.Value,
                    request.TimeZoneId),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        if (result.Outcome is CreateRouteOutcome.Created && request.ActiveTemplateIds is not null && result.RouteId.HasValue)
        {
            await loanTemplates.ConfigureForRouteAsync(actor, result.RouteId.Value, request.ActiveTemplateIds, cancellationToken);
        }

        return result.Outcome switch
        {
            CreateRouteOutcome.Created => Created($"/api/v1/routes/{result.RouteId}", new { id = result.RouteId }),
            CreateRouteOutcome.Invalid => BadRequest(new { message = result.Error }),
            CreateRouteOutcome.Conflict => Conflict(new { message = result.Error }),
            CreateRouteOutcome.Forbidden => Forbid(),
            _ => Problem("Unexpected route creation result.")
        };
    }

    [HttpPut("{routeId:guid}")]
    public async Task<IActionResult> Update(
        Guid routeId,
        UpdateRouteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        UpdateRouteResult result;
        try
        {
            result = await updateRoute.ExecuteAsync(
                new UpdateRouteCommand(
                    actor,
                    routeId,
                    request.Name,
                    request.Description,
                    request.Status,
                    request.SupervisorUserId,
                    request.CollectorUserIds ?? [],
                    request.MaxActiveCustomersMode,
                    request.MaxActiveCustomersOverride,
                    request.MaxActiveLoansMode,
                    request.MaxActiveLoansOverride,
                    request.TimeZoneId),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        if (result.Outcome is UpdateRouteOutcome.Updated && request.ActiveTemplateIds is not null)
        {
            await loanTemplates.ConfigureForRouteAsync(actor, routeId, request.ActiveTemplateIds, cancellationToken);
        }

        return result.Outcome switch
        {
            UpdateRouteOutcome.Updated => NoContent(),
            UpdateRouteOutcome.Invalid => BadRequest(new { message = result.Error }),
            UpdateRouteOutcome.Conflict => Conflict(new { message = result.Error }),
            UpdateRouteOutcome.NotFound => NotFound(),
            UpdateRouteOutcome.Forbidden => Forbid(),
            _ => Problem("Unexpected route update result.")
        };
    }

    [HttpGet("{routeId:guid}/loan-templates")]
    public async Task<IActionResult> ListRouteLoanTemplates(
        Guid routeId,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await loanTemplates.ListForRouteAsync(actor, routeId, cancellationToken);
        return result.Outcome switch
        {
            LoanTemplateOutcome.Success => Ok(result.Templates),
            LoanTemplateOutcome.NotFound => NotFound(new { message = result.Error }),
            LoanTemplateOutcome.Forbidden => Forbid(),
            _ => Problem("No fue posible consultar las plantillas de la ruta.")
        };
    }

    [HttpPut("{routeId:guid}/loan-templates")]
    public async Task<IActionResult> ConfigureRouteLoanTemplates(
        Guid routeId,
        ConfigureRouteLoanTemplatesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await loanTemplates.ConfigureForRouteAsync(actor, routeId, request.ActiveTemplateIds ?? [], cancellationToken);
        return result.Outcome switch
        {
            LoanTemplateOutcome.Success => Ok(result.Templates),
            LoanTemplateOutcome.NotFound => NotFound(new { message = result.Error }),
            LoanTemplateOutcome.Invalid => BadRequest(new { message = result.Error }),
            LoanTemplateOutcome.Forbidden => Forbid(),
            LoanTemplateOutcome.Conflict => Conflict(new { message = result.Error }),
            _ => Problem("No fue posible actualizar las plantillas de la ruta.")
        };
    }
}

public sealed record ConfigureRouteLoanTemplatesRequest(IReadOnlyCollection<Guid>? ActiveTemplateIds);

public sealed record CreateRouteRequest(
    string Name,
    string? Description,
    Guid SupervisorUserId,
    IReadOnlyCollection<Guid>? CollectorUserIds,
    decimal? InitialBalance,
    RouteSettingMode MaxActiveCustomersMode = RouteSettingMode.Inherited,
    int? MaxActiveCustomersOverride = null,
    RouteSettingMode MaxActiveLoansMode = RouteSettingMode.Inherited,
    int? MaxActiveLoansOverride = null,
    string? TimeZoneId = null,
    IReadOnlyCollection<Guid>? ActiveTemplateIds = null);

public sealed record UpdateRouteRequest(
    string Name,
    string? Description,
    CollectionRouteStatus Status,
    Guid SupervisorUserId,
    IReadOnlyCollection<Guid>? CollectorUserIds,
    RouteSettingMode MaxActiveCustomersMode,
    int? MaxActiveCustomersOverride,
    RouteSettingMode MaxActiveLoansMode,
    int? MaxActiveLoansOverride,
    string? TimeZoneId = null,
    IReadOnlyCollection<Guid>? ActiveTemplateIds = null);
