using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Configuration;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using System.Text.Json;

namespace MS.Cobroup.BackendAdmin.Core.Configuration;

public sealed class OrganizationSettingsService(IOrganizationSettingsRepository repository)
{
    public async Task<OrganizationSettingsResult> GetAsync(
        TenantActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return OrganizationSettingsResult.Forbidden();
        }

        var settings = await repository.FindAsync(actor.OrganizationId, cancellationToken);
        return OrganizationSettingsResult.Success(settings is null ? null : ToSummary(settings));
    }

    public async Task<OrganizationSettingsResult> UpdateAsync(
        UpdateOrganizationSettingsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Actor.Role is not UserRole.Admin)
        {
            return OrganizationSettingsResult.Forbidden();
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(command.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return OrganizationSettingsResult.Invalid("The time zone identifier is not recognized.");
        }
        catch (InvalidTimeZoneException)
        {
            return OrganizationSettingsResult.Invalid("The time zone data is invalid.");
        }

        var now = DateTimeOffset.UtcNow;
        var settings = await repository.FindAsync(command.Actor.OrganizationId, cancellationToken);
        var previous = settings is null ? null : new
        {
            settings.TimeZoneId,
            settings.CurrencyCode,
            settings.MaxActiveCustomersPerRoute,
            settings.MaxActiveLoansPerRoute
        };
        var isNew = settings is null;
        if (settings is null)
        {
            settings = OrganizationSettings.Create(
                command.Actor.OrganizationId,
                command.TimeZoneId,
                command.CurrencyCode,
                command.MaxActiveCustomersPerRoute,
                command.MaxActiveLoansPerRoute,
                command.Actor.UserId,
                now);
        }
        else
        {
            settings.Update(
                command.TimeZoneId,
                command.CurrencyCode,
                command.MaxActiveCustomersPerRoute,
                command.MaxActiveLoansPerRoute,
                command.Actor.UserId,
                now);
        }

        await repository.SaveAsync(
            settings,
            isNew,
            AdministrativeAuditRecord.OrganizationSettingsUpdated(
                command.Actor.OrganizationId,
                command.Actor.UserId,
                now,
                JsonSerializer.Serialize(new
                {
                    previous,
                    current = new
                    {
                        settings.TimeZoneId,
                        settings.CurrencyCode,
                        settings.MaxActiveCustomersPerRoute,
                        settings.MaxActiveLoansPerRoute
                    }
                })),
            cancellationToken);
        return OrganizationSettingsResult.Success(ToSummary(settings));
    }

    private static OrganizationSettingsSummary ToSummary(OrganizationSettings settings) =>
        new(
            settings.TimeZoneId,
            settings.CurrencyCode,
            settings.MaxActiveCustomersPerRoute,
            settings.MaxActiveLoansPerRoute,
            settings.UpdatedAtUtc);
}

public sealed record UpdateOrganizationSettingsCommand(
    TenantActor Actor,
    string TimeZoneId,
    string CurrencyCode,
    int? MaxActiveCustomersPerRoute,
    int? MaxActiveLoansPerRoute);

public sealed record OrganizationSettingsSummary(
    string TimeZoneId,
    string CurrencyCode,
    int? MaxActiveCustomersPerRoute,
    int? MaxActiveLoansPerRoute,
    DateTimeOffset UpdatedAtUtc);

public enum OrganizationSettingsOutcome
{
    Success,
    Invalid,
    Forbidden
}

public sealed record OrganizationSettingsResult(
    OrganizationSettingsOutcome Outcome,
    OrganizationSettingsSummary? Settings,
    string? Error)
{
    public static OrganizationSettingsResult Success(OrganizationSettingsSummary? settings) =>
        new(OrganizationSettingsOutcome.Success, settings, null);

    public static OrganizationSettingsResult Invalid(string error) =>
        new(OrganizationSettingsOutcome.Invalid, null, error);

    public static OrganizationSettingsResult Forbidden() =>
        new(OrganizationSettingsOutcome.Forbidden, null, null);
}
