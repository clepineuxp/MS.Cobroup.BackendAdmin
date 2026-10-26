using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfRouteManagementRepository(IdentityDbContext dbContext) : IRouteManagementRepository
{
    public async Task<IReadOnlyCollection<RouteSummary>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.OrganizationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);
        var routes = await dbContext.Routes
            .AsNoTracking()
            .Include(route => route.CollectorAssignments)
            .Where(route => route.OrganizationId == organizationId)
            .OrderBy(route => route.Name)
            .ToArrayAsync(cancellationToken);

        var userIds = routes
            .Select(route => route.SupervisorUserId)
            .Concat(routes.SelectMany(route => route.CollectorAssignments.Select(item => item.CollectorUserId)))
            .Distinct()
            .ToArray();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => new UserSummary(user.Id, user.Username, user.DisplayName),
                cancellationToken);

        return routes.Select(route => new RouteSummary(
                route.Id,
                route.Name,
                route.Description,
                route.Status,
                users[route.SupervisorUserId],
                route.CollectorAssignments
                    .Select(item => users[item.CollectorUserId])
                    .OrderBy(user => user.DisplayName)
                    .ToArray(),
                new RouteLimitSummary(
                    route.MaxActiveCustomersMode,
                    route.MaxActiveCustomersOverride,
                    route.ResolveMaxActiveCustomers(settings?.MaxActiveCustomersPerRoute)),
                new RouteLimitSummary(
                    route.MaxActiveLoansMode,
                    route.MaxActiveLoansOverride,
                    route.ResolveMaxActiveLoans(settings?.MaxActiveLoansPerRoute)),
                route.CreatedAtUtc,
                route.UpdatedAtUtc,
                route.InitialBalance))
            .ToArray();
    }

    public Task<bool> NameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludedRouteId,
        CancellationToken cancellationToken) =>
        dbContext.Routes.AnyAsync(
            route => route.OrganizationId == organizationId
                && route.NormalizedName == normalizedName
                && (!excludedRouteId.HasValue || route.Id != excludedRouteId.Value),
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, UserRole>> GetActiveUserRolesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.OrganizationId == organizationId
                && membership.IsActive
                && membership.User.IsActive
                && userIds.Contains(membership.UserId))
            .ToDictionaryAsync(
                membership => membership.UserId,
                membership => membership.Role,
                cancellationToken);

    public async Task<bool> TryAddAsync(
        CollectionRoute route,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Routes.Add(route);
        dbContext.AdministrativeAuditRecords.Add(auditRecord);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_routes_OrganizationId_NormalizedName"
            })
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    public Task<CollectionRoute?> FindAsync(
        Guid organizationId,
        Guid routeId,
        CancellationToken cancellationToken) =>
        dbContext.Routes
            .Include(route => route.CollectorAssignments)
            .SingleOrDefaultAsync(
                route => route.OrganizationId == organizationId && route.Id == routeId,
                cancellationToken);

    public async Task<bool> SaveChangesAsync(
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.AdministrativeAuditRecords.Add(auditRecord);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_routes_OrganizationId_NormalizedName"
            })
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    public async Task<IReadOnlyCollection<AccessibleRouteSummary>> ListAccessibleAsync(
        Guid organizationId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.OrganizationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);
        var query = dbContext.Routes
            .AsNoTracking()
            .Where(route => route.OrganizationId == organizationId && route.Status == CollectionRouteStatus.Active);
        query = role switch
        {
            UserRole.Admin => query,
            UserRole.Supervisor => query.Where(route => route.SupervisorUserId == userId),
            UserRole.Collector => query.Where(route => route.CollectorAssignments.Any(item => item.CollectorUserId == userId)),
            _ => query.Where(_ => false)
        };

        var routes = await query.OrderBy(route => route.Name).ToArrayAsync(cancellationToken);
        return routes.Select(route => new AccessibleRouteSummary(
                route.Id,
                route.Name,
                route.Description,
                role.ToString(),
                route.ResolveMaxActiveCustomers(settings?.MaxActiveCustomersPerRoute),
                route.ResolveMaxActiveLoans(settings?.MaxActiveLoansPerRoute),
                route.InitialBalance,
                settings?.TimeZoneId ?? "America/Bogota"))
            .ToArray();
    }
}
