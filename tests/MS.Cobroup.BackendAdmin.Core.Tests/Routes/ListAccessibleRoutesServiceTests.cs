using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Routes;

public sealed class ListAccessibleRoutesServiceTests
{
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Collector)]
    public async Task DelegatesTenantUserAndRoleToRepository(UserRole role)
    {
        var actor = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), role);
        var repository = new MemoryRouteRepository();

        var result = await new ListAccessibleRoutesService(repository).ExecuteAsync(actor, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(actor, repository.Actor);
    }

    private sealed class MemoryRouteRepository : IRouteManagementRepository
    {
        public TenantActor? Actor { get; private set; }
        public Task<IReadOnlyCollection<AccessibleRouteSummary>> ListAccessibleAsync(Guid organizationId, Guid userId, UserRole role, CancellationToken cancellationToken)
        {
            Actor = new TenantActor(userId, organizationId, role);
            IReadOnlyCollection<AccessibleRouteSummary> routes =
            [new(Guid.NewGuid(), "Centro", null, role.ToString(), 100, 100)];
            return Task.FromResult(routes);
        }
        public Task<IReadOnlyCollection<RouteSummary>> ListAsync(Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(Guid organizationId, string normalizedName, Guid? excludedRouteId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, UserRole>> GetActiveUserRolesAsync(Guid organizationId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryAddAsync(CollectionRoute route, AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CollectionRoute?> FindAsync(Guid organizationId, Guid routeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> SaveChangesAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
