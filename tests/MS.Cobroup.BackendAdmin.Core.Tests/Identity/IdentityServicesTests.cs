using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Identity;

public sealed class IdentityServicesTests
{
    [Fact]
    public async Task Bootstrap_CreatesExactlyOneOrganizationAdministrator()
    {
        var repository = new MemoryIdentityRepository();
        var service = new BootstrapIdentityService(repository, new DeterministicPasswordService());
        var command = CreateCommand();

        var first = await service.ExecuteAsync(command, CancellationToken.None);
        var second = await service.ExecuteAsync(command, CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal("ACME", repository.Organization?.Code);
        Assert.Equal(3, repository.Users.Count);
        Assert.Contains(repository.Users, user => user.NormalizedUsername == "ADMIN");
    }

    [Fact]
    public async Task Authenticate_RequiresMatchingOrganizationAndPassword()
    {
        var repository = new MemoryIdentityRepository();
        var passwords = new DeterministicPasswordService();
        var bootstrap = new BootstrapIdentityService(repository, passwords);
        await bootstrap.ExecuteAsync(
            CreateCommand(),
            CancellationToken.None);
        var authentication = new AuthenticateUserService(repository, passwords);

        var valid = await authentication.ExecuteAsync("acme", "ADMIN", "a-strong-password", CancellationToken.None);
        var wrongTenant = await authentication.ExecuteAsync("other", "admin", "a-strong-password", CancellationToken.None);
        var wrongPassword = await authentication.ExecuteAsync("acme", "admin", "wrong-password", CancellationToken.None);

        Assert.NotNull(valid);
        Assert.Null(wrongTenant);
        Assert.Null(wrongPassword);
    }

    private sealed class DeterministicPasswordService : IPasswordCredentialService
    {
        public string Hash(string password) => $"hashed::{password}";

        public bool Verify(ApplicationUser user, string password) => user.PasswordHash == $"hashed::{password}";
    }

    private sealed class MemoryIdentityRepository : IIdentityRepository
    {
        public Organization? Organization { get; private set; }

        public List<ApplicationUser> Users { get; } = [];

        public Task<bool> TryBootstrapAsync(
            Organization organization,
            IReadOnlyCollection<ApplicationUser> users,
            SecurityAuditRecord auditRecord,
            CancellationToken cancellationToken)
        {
            if (Organization is not null)
            {
                return Task.FromResult(false);
            }

            Organization = organization;
            Users.AddRange(users);
            return Task.FromResult(true);
        }

        public Task<ApplicationUser?> FindActiveUserAsync(
            string organizationCode,
            string normalizedUsername,
            CancellationToken cancellationToken)
        {
            var match = Organization?.Code == organizationCode
                ? Users.SingleOrDefault(user => user.NormalizedUsername == normalizedUsername)
                : null;
            return Task.FromResult(match);
        }
    }

    private static BootstrapIdentityCommand CreateCommand() => new(
        " acme ",
        "Acme",
        new BootstrapUserCommand(" admin ", "Administrator", "a-strong-password"),
        new BootstrapUserCommand("supervisor", "Supervisor", "another-strong-password"),
        new BootstrapUserCommand("collector", "Collector", "collector-strong-password"),
        "127.0.0.1");
}
