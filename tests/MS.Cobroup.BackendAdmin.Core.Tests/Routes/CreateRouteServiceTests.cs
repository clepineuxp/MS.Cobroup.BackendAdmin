using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Routes;

public sealed class CreateRouteServiceTests
{
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid administratorId = Guid.NewGuid();
    private readonly Guid supervisorId = Guid.NewGuid();
    private readonly Guid collectorId = Guid.NewGuid();

    [Fact]
    public async Task AdminCreatesRouteWithSameTenantAssignmentsAndAudit()
    {
        var repository = CreateRepository();
        var service = new CreateRouteService(repository);

        var result = await service.ExecuteAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(CreateRouteOutcome.Created, result.Outcome);
        Assert.Equal(organizationId, repository.Route?.OrganizationId);
        Assert.Equal(supervisorId, repository.Route?.SupervisorUserId);
        Assert.Equal([collectorId], repository.Route?.CollectorAssignments.Select(item => item.CollectorUserId));
        Assert.Equal(repository.Route?.Id, repository.Audit?.EntityId);
        Assert.Equal(organizationId, repository.Audit?.OrganizationId);
    }

    [Fact]
    public async Task NonAdminCannotCreateRoute()
    {
        var repository = CreateRepository();
        var service = new CreateRouteService(repository);
        var command = CreateCommand() with
        {
            Actor = new TenantActor(collectorId, organizationId, UserRole.Collector)
        };

        var result = await service.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(CreateRouteOutcome.Forbidden, result.Outcome);
        Assert.Null(repository.Route);
    }

    [Fact]
    public async Task UserFromAnotherTenantCannotBeAssigned()
    {
        var repository = CreateRepository();
        repository.UserRoles.Remove(supervisorId);
        var service = new CreateRouteService(repository);

        var result = await service.ExecuteAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(CreateRouteOutcome.Invalid, result.Outcome);
        Assert.Null(repository.Route);
    }

    [Fact]
    public async Task DuplicateNameWithinTenantIsRejected()
    {
        var repository = CreateRepository();
        repository.ExistingNames.Add("CENTRO");
        var service = new CreateRouteService(repository);

        var result = await service.ExecuteAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(CreateRouteOutcome.Conflict, result.Outcome);
        Assert.Null(repository.Route);
    }

    private MemoryRouteRepository CreateRepository()
    {
        var repository = new MemoryRouteRepository();
        repository.UserRoles[supervisorId] = UserRole.Supervisor;
        repository.UserRoles[collectorId] = UserRole.Collector;
        return repository;
    }

    private CreateRouteCommand CreateCommand() => new(
        new TenantActor(administratorId, organizationId, UserRole.Admin),
        " Centro ",
        "Ruta principal",
        supervisorId,
        [collectorId]);

    private sealed class MemoryRouteRepository : IRouteManagementRepository
    {
        public HashSet<string> ExistingNames { get; } = [];

        public Dictionary<Guid, UserRole> UserRoles { get; } = [];

        public CollectionRoute? Route { get; private set; }

        public AdministrativeAuditRecord? Audit { get; private set; }

        public Task<IReadOnlyCollection<RouteSummary>> ListAsync(
            Guid organizationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<RouteSummary>>([]);

        public Task<bool> NameExistsAsync(
            Guid organizationId,
            string normalizedName,
            Guid? excludedRouteId,
            CancellationToken cancellationToken) =>
            Task.FromResult(ExistingNames.Contains(normalizedName));

        public Task<IReadOnlyDictionary<Guid, UserRole>> GetActiveUserRolesAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<Guid, UserRole> matches = UserRoles
                .Where(pair => userIds.Contains(pair.Key))
                .ToDictionary();
            return Task.FromResult(matches);
        }

        public Task<bool> TryAddAsync(
            CollectionRoute route,
            AdministrativeAuditRecord auditRecord,
            CancellationToken cancellationToken)
        {
            Route = route;
            Audit = auditRecord;
            return Task.FromResult(true);
        }

        public Task<CollectionRoute?> FindAsync(Guid organizationId, Guid routeId, CancellationToken cancellationToken) =>
            Task.FromResult<CollectionRoute?>(null);

        public Task<bool> SaveChangesAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlyCollection<AccessibleRouteSummary>> ListAccessibleAsync(Guid organizationId, Guid userId, UserRole role, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<AccessibleRouteSummary>>([]);
    }
}
