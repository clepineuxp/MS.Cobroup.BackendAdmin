using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.MachineAuth;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/machine-auth")]
public sealed class MachineAuthController(MachineAuthorizationService service) : ControllerBase
{
    [HttpPost("otp")][Authorize(Policy = "tenant-user")]
    public async Task<IActionResult> RequestOtp(MachineAuthOtpRequest request, CancellationToken ct)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        try
        {
            var result = await service.RequestOtpAsync(actor, request.RouteId, request.MachineId, ct);
            return result.Success ? Ok(new { requestId = result.RequestId, otp = result.Otp, expiresAtUtc = result.ExpiresAtUtc }) : Forbid();
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("check")][Authorize(Policy = "tenant-user")]
    public async Task<IActionResult> Check([FromQuery] Guid routeId, [FromHeader(Name = "X-Machine-Id")] string machineId, CancellationToken ct)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        var result = await service.CheckAsync(actor.OrganizationId, routeId, actor.UserId, machineId, ct);
        return result.Allowed ? Ok(result) : StatusCode(StatusCodes.Status428PreconditionRequired, new { code = "machine_authorization_required", message = "Esta máquina aún no está autorizada para la ruta." });
    }

    [HttpGet("pending")][Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Pending([FromQuery] Guid? routeId, CancellationToken ct)
    { return TenantActorReader.TryRead(User, out var actor) ? Ok(await service.ListPendingAsync(actor, routeId, ct)) : Unauthorized(); }

    [HttpGet("authorizations")][Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Authorizations([FromQuery] Guid routeId, CancellationToken ct)
    { return TenantActorReader.TryRead(User, out var actor) ? Ok(await service.ListAsync(actor, routeId, ct)) : Unauthorized(); }

    [HttpPost("approve")][Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Approve(ApproveMachineAuthRequest request, CancellationToken ct)
    {
        if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized();
        var result = await service.ApproveAsync(actor, request.RequestId, request.Otp, request.Days, request.Indefinite, ct);
        return result is null ? BadRequest(new { message = "OTP inválido, expirado o duración no válida." }) : Ok(result);
    }

    [HttpPost("{id:guid}/revoke")][Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    { if (!TenantActorReader.TryRead(User, out var actor)) return Unauthorized(); return await service.RevokeAsync(actor, id, ct) ? NoContent() : NotFound(); }
}

public sealed record MachineAuthOtpRequest(Guid RouteId, string MachineId);
public sealed record ApproveMachineAuthRequest(Guid RequestId, string Otp, int? Days, bool Indefinite);
