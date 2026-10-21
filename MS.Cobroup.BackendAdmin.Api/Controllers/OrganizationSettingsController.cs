using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Core.Configuration;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/organization-settings")]
[Authorize(Policy = "tenant-admin")]
public sealed class OrganizationSettingsController(OrganizationSettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await settings.GetAsync(actor, cancellationToken);
        return result.Outcome is OrganizationSettingsOutcome.Success
            ? Ok(new { configured = result.Settings is not null, settings = result.Settings })
            : Forbid();
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        UpdateOrganizationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TenantActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        OrganizationSettingsResult result;
        try
        {
            result = await settings.UpdateAsync(
                new UpdateOrganizationSettingsCommand(
                    actor,
                    request.TimeZoneId,
                    request.CurrencyCode,
                    request.MaxActiveCustomersPerRoute,
                    request.MaxActiveLoansPerRoute),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        return result.Outcome switch
        {
            OrganizationSettingsOutcome.Success => Ok(result.Settings),
            OrganizationSettingsOutcome.Invalid => BadRequest(new { message = result.Error }),
            OrganizationSettingsOutcome.Forbidden => Forbid(),
            _ => Problem("Unexpected organization settings result.")
        };
    }
}

public sealed record UpdateOrganizationSettingsRequest(
    string TimeZoneId,
    string CurrencyCode,
    int? MaxActiveCustomersPerRoute,
    int? MaxActiveLoansPerRoute);
