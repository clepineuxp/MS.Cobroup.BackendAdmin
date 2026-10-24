using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Core.Users;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Users;

public sealed class UserManagementServiceTests
{
    private readonly MemoryRepository repository = new();
    private readonly FakePasswords passwords = new();
    private readonly TenantActor admin = new(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);
    private static CreateOrganizationUserInput Input(UserRole role = UserRole.Collector) =>
        new(" cobrador.norte ", " Ana Pérez ", "long-test-password", role, "+57 300 1234567");

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Collector)]
    public async Task CreatesOnlyOperationalMembershipInAuthenticatedOrganization(UserRole role)
    {
        var result = await new UserManagementService(repository, passwords).CreateAsync(admin, Input(role), default);
        Assert.Equal(UserManagementOutcome.Success, result.Outcome);
        Assert.Equal(admin.OrganizationId, repository.Membership!.OrganizationId);
        Assert.Equal(role, repository.Membership.Role);
        Assert.False(repository.Membership.IsOwner);
        Assert.Equal("COBRADOR.NORTE", repository.User!.NormalizedUsername);
        Assert.Equal("hash", repository.User.PasswordHash);
        Assert.Equal(repository.User.CreatedAtUtc, repository.User.UpdatedAtUtc);
        Assert.Equal("+57 300 1234567", result.User!.PhoneNumber);
        Assert.Equal(admin.UserId, repository.Audit!.ActorUserId);
        Assert.Equal(admin.OrganizationId, repository.Audit.OrganizationId);
        Assert.DoesNotContain(Input().Password, repository.Audit.DetailsJson ?? "");
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Collector)]
    public async Task NonAdminCannotReadOrCreate(UserRole role)
    {
        var actor = admin with { Role = role };
        var service = new UserManagementService(repository, passwords);
        Assert.Equal(UserManagementOutcome.Forbidden, (await service.CreateAsync(actor, Input(), default)).Outcome);
        Assert.Equal(UserManagementOutcome.Forbidden, (await service.ListAsync(actor, default)).Outcome);
        Assert.Null(repository.User);
        Assert.Equal(0, passwords.Calls);
    }

    [Fact]
    public async Task InactiveAdminCannotManageEvenWithAdminToken()
    {
        repository.ActiveAdmin = false;
        var service = new UserManagementService(repository, passwords);
        Assert.Equal(UserManagementOutcome.Forbidden, (await service.CreateAsync(admin, Input(), default)).Outcome);
        Assert.Equal(UserManagementOutcome.Forbidden, (await service.ListAsync(admin, default)).Outcome);
        Assert.Null(repository.User);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData((UserRole)123)]
    public async Task RejectsPrivilegeEscalation(UserRole role)
    {
        var result = await new UserManagementService(repository, passwords).CreateAsync(admin, Input(role), default);
        Assert.Equal(UserManagementOutcome.Invalid, result.Outcome);
        Assert.Null(repository.User);
        Assert.Equal(0, passwords.Calls);
    }

    [Theory]
    [InlineData("short", "+573001234567")]
    [InlineData("long-test-password", "invalid")]
    [InlineData("long-test-password", "123")]
    public async Task RejectsInvalidCredentialsBeforeHashOrSave(string password, string phone)
    {
        var input = Input() with { Password = password, PhoneNumber = phone };
        var result = await new UserManagementService(repository, passwords).CreateAsync(admin, input, default);
        Assert.Equal(UserManagementOutcome.Invalid, result.Outcome);
        Assert.Equal(0, passwords.Calls);
        Assert.Null(repository.User);
    }

    [Fact]
    public async Task DuplicateGlobalUsernameReturnsConflictWithoutCredentials()
    {
        repository.CreateSucceeds = false;
        var result = await new UserManagementService(repository, passwords).CreateAsync(admin, Input(), default);
        Assert.Equal(UserManagementOutcome.Conflict, result.Outcome);
        Assert.Null(result.User);
        Assert.DoesNotContain(Input().Password, result.Error!);
    }

    [Fact]
    public async Task ListingAlwaysUsesActorOrganization()
    {
        await new UserManagementService(repository, passwords).ListAsync(admin, default);
        Assert.Equal(admin.OrganizationId, repository.ListedOrganization);
    }

    private sealed class MemoryRepository : IUserManagementRepository
    {
        public bool ActiveAdmin { get; set; } = true;
        public bool CreateSucceeds { get; set; } = true;
        public Guid? ListedOrganization { get; private set; }
        public ApplicationUser? User { get; private set; }
        public OrganizationMembership? Membership { get; private set; }
        public AdministrativeAuditRecord? Audit { get; private set; }
        public Task<bool> IsActiveAdminAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(ActiveAdmin);
        public Task<IReadOnlyCollection<OrganizationUser>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            ListedOrganization = organizationId;
            return Task.FromResult<IReadOnlyCollection<OrganizationUser>>([]);
        }
        public Task<bool> TryCreateAsync(ApplicationUser user, OrganizationMembership membership, AdministrativeAuditRecord audit, CancellationToken cancellationToken)
        {
            User = user; Membership = membership; Audit = audit;
            return Task.FromResult(CreateSucceeds);
        }
    }
    private sealed class FakePasswords : IPasswordCredentialService
    {
        public int Calls { get; private set; }
        public string Hash(string password) { Calls++; return "hash"; }
        public bool Verify(ApplicationUser user, string password) => false;
    }
}
