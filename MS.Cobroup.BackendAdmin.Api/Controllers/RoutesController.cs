using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/routes")]
[Authorize(Policy = "tenant-admin")]
public sealed class RoutesController(
    CreateRouteService createRoute,
    ListRoutesService listRoutes,
    UpdateRouteService updateRoute) : ControllerBase
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
                    request.InitialBalance.Value),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
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
                    request.MaxActiveLoansOverride),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
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

}

public sealed record CreateRouteRequest(
    string Name,
    string? Description,
    Guid SupervisorUserId,
    IReadOnlyCollection<Guid>? CollectorUserIds,
    decimal? InitialBalance,
    RouteSettingMode MaxActiveCustomersMode = RouteSettingMode.Inherited,
    int? MaxActiveCustomersOverride = null,
    RouteSettingMode MaxActiveLoansMode = RouteSettingMode.Inherited,
    int? MaxActiveLoansOverride = null);

public sealed record UpdateRouteRequest(
    string Name,
    string? Description,
    CollectionRouteStatus Status,
    Guid SupervisorUserId,
    IReadOnlyCollection<Guid>? CollectorUserIds,
    RouteSettingMode MaxActiveCustomersMode,
    int? MaxActiveCustomersOverride,
    RouteSettingMode MaxActiveLoansMode,
    int? MaxActiveLoansOverride);
