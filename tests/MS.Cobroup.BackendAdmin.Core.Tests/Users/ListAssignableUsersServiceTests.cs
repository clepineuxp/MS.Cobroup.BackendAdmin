using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Core.Users;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Users;

public sealed class ListAssignableUsersServiceTests
{
    [Fact]
    public async Task AdminListsAssignableUsersForActorOrganization()
    {
        var organizationId = Guid.NewGuid();
        var repository = new MemoryUserDirectoryRepository();
        var service = new ListAssignableUsersService(repository);

        var result = await service.ExecuteAsync(
            new TenantActor(Guid.NewGuid(), organizationId, UserRole.Admin),
            CancellationToken.None);

        Assert.Equal(ListAssignableUsersOutcome.Success, result.Outcome);
        Assert.Equal(organizationId, repository.RequestedOrganizationId);
        Assert.Collection(result.Users,
            user => Assert.Equal(UserRole.Supervisor, user.Role),
            user => Assert.Equal(UserRole.Collector, user.Role));
    }

    [Fact]
    public async Task NonAdminCannotListAssignableUsers()
    {
        var repository = new MemoryUserDirectoryRepository();
        var service = new ListAssignableUsersService(repository);

        var result = await service.ExecuteAsync(
            new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Collector),
            CancellationToken.None);

        Assert.Equal(ListAssignableUsersOutcome.Forbidden, result.Outcome);
        Assert.Null(repository.RequestedOrganizationId);
    }

    private sealed class MemoryUserDirectoryRepository : IUserDirectoryRepository
    {
        public Guid? RequestedOrganizationId { get; private set; }

        public Task<IReadOnlyCollection<AssignableUser>> ListAssignableAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
        {
            RequestedOrganizationId = organizationId;
            IReadOnlyCollection<AssignableUser> users =
            [
                new(Guid.NewGuid(), "supervisor", "Supervisor", UserRole.Supervisor),
                new(Guid.NewGuid(), "collector", "Collector", UserRole.Collector)
            ];
            return Task.FromResult(users);
        }
    }
}
