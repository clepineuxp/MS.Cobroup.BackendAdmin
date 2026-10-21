using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Routes;

public sealed class ListRoutesServiceTests
{
    [Fact]
    public async Task AdminListsRoutesOnlyForActorOrganization()
    {
        var organizationId = Guid.NewGuid();
        var repository = new MemoryRouteRepository();
        var service = new ListRoutesService(repository);

        var result = await service.ExecuteAsync(
            new TenantActor(Guid.NewGuid(), organizationId, UserRole.Admin),
            CancellationToken.None);

        Assert.Equal(ListRoutesOutcome.Success, result.Outcome);
        Assert.Equal(organizationId, repository.RequestedOrganizationId);
        Assert.Single(result.Routes);
    }

    [Fact]
    public async Task NonAdminCannotListRoutes()
    {
        var repository = new MemoryRouteRepository();
        var service = new ListRoutesService(repository);

        var result = await service.ExecuteAsync(
            new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Supervisor),
            CancellationToken.None);

        Assert.Equal(ListRoutesOutcome.Forbidden, result.Outcome);
        Assert.Null(repository.RequestedOrganizationId);
    }

    private sealed class MemoryRouteRepository : IRouteManagementRepository
    {
        public Guid? RequestedOrganizationId { get; private set; }

        public Task<IReadOnlyCollection<RouteSummary>> ListAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
        {
            RequestedOrganizationId = organizationId;
            IReadOnlyCollection<RouteSummary> routes =
            [
                new(
                    Guid.NewGuid(),
                    "Centro",
                    null,
                    CollectionRouteStatus.Active,
                    new UserSummary(Guid.NewGuid(), "supervisor", "Supervisor"),
                    [new UserSummary(Guid.NewGuid(), "collector", "Collector")],
                    new RouteLimitSummary(RouteSettingMode.Inherited, null, 100),
                    new RouteLimitSummary(RouteSettingMode.Inherited, null, 100),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow)
            ];
            return Task.FromResult(routes);
        }

        public Task<bool> NameExistsAsync(Guid organizationId, string normalizedName, Guid? excludedRouteId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, UserRole>> GetActiveUserRolesAsync(Guid organizationId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryAddAsync(CollectionRoute route, AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CollectionRoute?> FindAsync(Guid organizationId, Guid routeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> SaveChangesAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<AccessibleRouteSummary>> ListAccessibleAsync(Guid organizationId, Guid userId, UserRole role, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
