using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Routes;

public sealed class UpdateRouteServiceTests
{
    [Fact]
    public async Task AdminUpdatesAssignmentsStatusAndOverridesWithinTenant()
    {
        var organizationId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var supervisorId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        var route = CollectionRoute.Create(organizationId, "Centro", null, supervisorId, [collectorId], adminId, DateTimeOffset.UtcNow);
        var repository = new MemoryRouteRepository(route, supervisorId, collectorId);
        var service = new UpdateRouteService(repository);

        var result = await service.ExecuteAsync(
            new UpdateRouteCommand(
                new TenantActor(adminId, organizationId, UserRole.Admin),
                route.Id,
                "Centro norte",
                "Actualizada",
                CollectionRouteStatus.Inactive,
                supervisorId,
                [collectorId],
                RouteSettingMode.Overridden,
                25,
                RouteSettingMode.Inherited,
                null),
            CancellationToken.None);

        Assert.Equal(UpdateRouteOutcome.Updated, result.Outcome);
        Assert.Equal(CollectionRouteStatus.Inactive, route.Status);
        Assert.Equal(25, route.MaxActiveCustomersOverride);
        Assert.Equal("route.updated", repository.Audit?.EventType);
        Assert.Contains(supervisorId.ToString(), repository.Audit?.DetailsJson);
        Assert.Contains(collectorId.ToString(), repository.Audit?.DetailsJson);
    }

    [Fact]
    public async Task RouteFromAnotherTenantIsNotFound()
    {
        var repository = new MemoryRouteRepository(null, Guid.NewGuid(), Guid.NewGuid());
        var result = await new UpdateRouteService(repository).ExecuteAsync(
            new UpdateRouteCommand(
                new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin),
                Guid.NewGuid(),
                "Ruta",
                null,
                CollectionRouteStatus.Active,
                Guid.NewGuid(),
                [Guid.NewGuid()],
                RouteSettingMode.Inherited,
                null,
                RouteSettingMode.Inherited,
                null),
            CancellationToken.None);

        Assert.Equal(UpdateRouteOutcome.NotFound, result.Outcome);
    }

    private sealed class MemoryRouteRepository(
        CollectionRoute? route,
        Guid supervisorId,
        Guid collectorId) : IRouteManagementRepository
    {
        public AdministrativeAuditRecord? Audit { get; private set; }
        public Task<IReadOnlyCollection<RouteSummary>> ListAsync(Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(Guid organizationId, string normalizedName, Guid? excludedRouteId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyDictionary<Guid, UserRole>> GetActiveUserRolesAsync(Guid organizationId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, UserRole>>(new Dictionary<Guid, UserRole> { [supervisorId] = UserRole.Supervisor, [collectorId] = UserRole.Collector });
        public Task<bool> TryAddAsync(CollectionRoute item, AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CollectionRoute?> FindAsync(Guid organizationId, Guid routeId, CancellationToken cancellationToken) => Task.FromResult(route);
        public Task<bool> SaveChangesAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken) { Audit = auditRecord; return Task.FromResult(true); }
        public Task<IReadOnlyCollection<AccessibleRouteSummary>> ListAccessibleAsync(Guid organizationId, Guid userId, UserRole role, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
