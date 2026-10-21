using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using System.Text.Json;

namespace MS.Cobroup.BackendAdmin.Core.Routes;

public sealed class UpdateRouteService(IRouteManagementRepository repository)
{
    public async Task<UpdateRouteResult> ExecuteAsync(
        UpdateRouteCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Actor.Role is not UserRole.Admin)
        {
            return UpdateRouteResult.Forbidden();
        }

        var route = await repository.FindAsync(command.Actor.OrganizationId, command.RouteId, cancellationToken);
        if (route is null)
        {
            return UpdateRouteResult.NotFound();
        }

        var normalizedName = CollectionRoute.NormalizeName(command.Name);
        if (await repository.NameExistsAsync(
                command.Actor.OrganizationId,
                normalizedName,
                command.RouteId,
                cancellationToken))
        {
            return UpdateRouteResult.Conflict("A route with this name already exists in the organization.");
        }

        if (command.CollectorUserIds.Count is 0)
        {
            return UpdateRouteResult.Invalid("At least one collector is required.");
        }

        var requestedUserIds = command.CollectorUserIds
            .Append(command.SupervisorUserId)
            .Distinct()
            .ToArray();
        var roles = await repository.GetActiveUserRolesAsync(
            command.Actor.OrganizationId,
            requestedUserIds,
            cancellationToken);
        if (!roles.TryGetValue(command.SupervisorUserId, out var supervisorRole)
            || supervisorRole is not UserRole.Supervisor)
        {
            return UpdateRouteResult.Invalid("The supervisor must be an active Supervisor in the same organization.");
        }

        if (command.CollectorUserIds.Any(userId =>
                !roles.TryGetValue(userId, out var role) || role is not UserRole.Collector))
        {
            return UpdateRouteResult.Invalid("Every collector must be an active Collector in the same organization.");
        }

        var now = DateTimeOffset.UtcNow;
        var previous = new
        {
            route.Name,
            route.Description,
            route.Status,
            route.SupervisorUserId,
            CollectorUserIds = route.CollectorAssignments.Select(item => item.CollectorUserId).Order().ToArray(),
            route.MaxActiveCustomersMode,
            route.MaxActiveCustomersOverride,
            route.MaxActiveLoansMode,
            route.MaxActiveLoansOverride
        };
        route.Update(
            command.Name,
            command.Description,
            command.Status,
            command.SupervisorUserId,
            command.CollectorUserIds,
            command.MaxActiveCustomersMode,
            command.MaxActiveCustomersOverride,
            command.MaxActiveLoansMode,
            command.MaxActiveLoansOverride,
            command.Actor.UserId,
            now);
        var current = new
        {
            route.Name,
            route.Description,
            route.Status,
            route.SupervisorUserId,
            CollectorUserIds = route.CollectorAssignments.Select(item => item.CollectorUserId).Order().ToArray(),
            route.MaxActiveCustomersMode,
            route.MaxActiveCustomersOverride,
            route.MaxActiveLoansMode,
            route.MaxActiveLoansOverride
        };
        var updated = await repository.SaveChangesAsync(
            AdministrativeAuditRecord.RouteUpdated(
                command.Actor.OrganizationId,
                command.Actor.UserId,
                route.Id,
                now,
                JsonSerializer.Serialize(new { previous, current })),
            cancellationToken);
        return updated
            ? UpdateRouteResult.Updated()
            : UpdateRouteResult.Conflict("A route with this name already exists in the organization.");
    }
}

public sealed record UpdateRouteCommand(
    TenantActor Actor,
    Guid RouteId,
    string Name,
    string? Description,
    CollectionRouteStatus Status,
    Guid SupervisorUserId,
    IReadOnlyCollection<Guid> CollectorUserIds,
    RouteSettingMode MaxActiveCustomersMode,
    int? MaxActiveCustomersOverride,
    RouteSettingMode MaxActiveLoansMode,
    int? MaxActiveLoansOverride);

public enum UpdateRouteOutcome
{
    Updated,
    Invalid,
    Conflict,
    NotFound,
    Forbidden
}

public sealed record UpdateRouteResult(UpdateRouteOutcome Outcome, string? Error)
{
    public static UpdateRouteResult Updated() => new(UpdateRouteOutcome.Updated, null);
    public static UpdateRouteResult Invalid(string error) => new(UpdateRouteOutcome.Invalid, error);
    public static UpdateRouteResult Conflict(string error) => new(UpdateRouteOutcome.Conflict, error);
    public static UpdateRouteResult NotFound() => new(UpdateRouteOutcome.NotFound, null);
    public static UpdateRouteResult Forbidden() => new(UpdateRouteOutcome.Forbidden, null);
}
