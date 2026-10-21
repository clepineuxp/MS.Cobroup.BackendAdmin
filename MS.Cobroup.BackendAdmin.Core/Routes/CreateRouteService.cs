using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Routes;

public sealed class CreateRouteService(IRouteManagementRepository repository)
{
    public async Task<CreateRouteResult> ExecuteAsync(
        CreateRouteCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Actor.Role is not UserRole.Admin)
        {
            return CreateRouteResult.Forbidden();
        }

        var normalizedName = CollectionRoute.NormalizeName(command.Name);
        if (await repository.NameExistsAsync(command.Actor.OrganizationId, normalizedName, null, cancellationToken))
        {
            return CreateRouteResult.Conflict("A route with this name already exists in the organization.");
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
            return CreateRouteResult.Invalid("The supervisor must be an active Supervisor in the same organization.");
        }

        if (command.CollectorUserIds.Count is 0)
        {
            return CreateRouteResult.Invalid("At least one collector is required.");
        }

        if (command.CollectorUserIds.Any(userId =>
            !roles.TryGetValue(userId, out var role) || role is not UserRole.Collector))
        {
            return CreateRouteResult.Invalid("Every collector must be an active Collector in the same organization.");
        }

        var now = DateTimeOffset.UtcNow;
        var route = CollectionRoute.Create(
            command.Actor.OrganizationId,
            command.Name,
            command.Description,
            command.SupervisorUserId,
            command.CollectorUserIds,
            command.Actor.UserId,
            now,
            command.MaxActiveCustomersMode,
            command.MaxActiveCustomersOverride,
            command.MaxActiveLoansMode,
            command.MaxActiveLoansOverride);
        var audit = AdministrativeAuditRecord.RouteCreated(
            command.Actor.OrganizationId,
            command.Actor.UserId,
            route.Id,
            now);

        var created = await repository.TryAddAsync(route, audit, cancellationToken);
        return created
            ? CreateRouteResult.Created(route.Id)
            : CreateRouteResult.Conflict("A route with this name already exists in the organization.");
    }
}

public sealed record TenantActor(Guid UserId, Guid OrganizationId, UserRole Role);

public sealed record CreateRouteCommand(
    TenantActor Actor,
    string Name,
    string? Description,
    Guid SupervisorUserId,
    IReadOnlyCollection<Guid> CollectorUserIds,
    RouteSettingMode MaxActiveCustomersMode = RouteSettingMode.Inherited,
    int? MaxActiveCustomersOverride = null,
    RouteSettingMode MaxActiveLoansMode = RouteSettingMode.Inherited,
    int? MaxActiveLoansOverride = null);

public enum CreateRouteOutcome
{
    Created,
    Invalid,
    Conflict,
    Forbidden
}

public sealed record CreateRouteResult(CreateRouteOutcome Outcome, Guid? RouteId, string? Error)
{
    public static CreateRouteResult Created(Guid routeId) => new(CreateRouteOutcome.Created, routeId, null);

    public static CreateRouteResult Invalid(string error) => new(CreateRouteOutcome.Invalid, null, error);

    public static CreateRouteResult Conflict(string error) => new(CreateRouteOutcome.Conflict, null, error);

    public static CreateRouteResult Forbidden() => new(CreateRouteOutcome.Forbidden, null, null);
}
