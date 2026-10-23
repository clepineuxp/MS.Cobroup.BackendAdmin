using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

public interface IRouteManagementRepository
{
    Task<IReadOnlyCollection<RouteSummary>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludedRouteId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, UserRole>> GetActiveUserRolesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    Task<bool> TryAddAsync(
        CollectionRoute route,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken);

    Task<CollectionRoute?> FindAsync(
        Guid organizationId,
        Guid routeId,
        CancellationToken cancellationToken);

    Task<bool> SaveChangesAsync(
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AccessibleRouteSummary>> ListAccessibleAsync(
        Guid organizationId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken);
}

public sealed record RouteSummary(
    Guid Id,
    string Name,
    string? Description,
    CollectionRouteStatus Status,
    UserSummary Supervisor,
    IReadOnlyCollection<UserSummary> Collectors,
    RouteLimitSummary MaxActiveCustomers,
    RouteLimitSummary MaxActiveLoans,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record UserSummary(Guid Id, string Username, string DisplayName);

public sealed record RouteLimitSummary(RouteSettingMode Mode, int? OverrideValue, int? EffectiveValue);

public sealed record AccessibleRouteSummary(
    Guid Id,
    string Name,
    string? Description,
    string AccessAs,
    int? MaxActiveCustomers,
    int? MaxActiveLoans);
