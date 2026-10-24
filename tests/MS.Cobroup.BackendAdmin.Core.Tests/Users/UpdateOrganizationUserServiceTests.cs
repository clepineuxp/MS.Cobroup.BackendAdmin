using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Core.Users;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Users;

public sealed class UpdateOrganizationUserServiceTests
{
    private readonly Repository repository = new();
    private readonly Passwords passwords = new();
    private readonly TenantActor admin;
    private readonly ApplicationUser user;
    private readonly OrganizationMembership membership;
    private UpdateOrganizationUserService Service => new(repository, repository, passwords);
    private UpdateOrganizationUserInput Input => new(user.Username, "Updated", "+573009876543", UserRole.Collector, true);

    public UpdateOrganizationUserServiceTests()
    {
        admin = new(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);
        user = ApplicationUser.Create("collector", "Collector", "old-hash", DateTimeOffset.UtcNow, "+573001234567");
        membership = OrganizationMembership.Create(user.Id, admin.OrganizationId, UserRole.Collector, DateTimeOffset.UtcNow);
        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.User))!.SetValue(membership, user);
        repository.Member = membership;
    }

    [Fact]
    public async Task UpdatesProfileKeepsPasswordAndRecordsAuditWithoutSecrets()
    {
        var result = await Service.ExecuteAsync(admin, user.Id, Input, default);
        Assert.Equal(UserUpdateOutcome.Success, result.Outcome);
        Assert.Equal("Updated", user.DisplayName); Assert.Equal("old-hash", user.PasswordHash);
        Assert.Equal(0, passwords.Calls); Assert.Equal("user.updated", repository.Audit!.EventType);
        Assert.DoesNotContain("old-hash", repository.Audit.DetailsJson!);
        Assert.Equal(admin.OrganizationId, repository.Audit.OrganizationId);
    }

    [Fact]
    public async Task PasswordChangeHashesAndRequestsRevocationOfAllSessions()
    {
        var result = await Service.ExecuteAsync(admin, user.Id, Input with { Password = "1" }, default);
        Assert.Equal(UserUpdateOutcome.Success, result.Outcome); Assert.Equal("new-hash", user.PasswordHash);
        Assert.True(repository.RevokeAll); Assert.Equal(1, passwords.Calls);
        Assert.DoesNotContain("new-hash", repository.Audit!.DetailsJson!);
    }

    [Fact]
    public async Task CrossTenantTargetReturnsNotFoundWithoutMutations()
    {
        var result = await Service.ExecuteAsync(admin with { OrganizationId = Guid.NewGuid() }, user.Id, Input, default);
        Assert.Equal(UserUpdateOutcome.NotFound, result.Outcome); Assert.Null(repository.Audit);
        Assert.Equal("Collector", user.DisplayName);
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Collector)]
    public async Task OperationalRolesCannotEdit(UserRole role)
    {
        Assert.Equal(UserUpdateOutcome.Forbidden, (await Service.ExecuteAsync(admin with { Role = role }, user.Id, Input, default)).Outcome);
        Assert.Null(repository.Audit);
    }

    [Fact]
    public async Task InactiveAdministratorCannotEdit()
    {
        repository.ActiveAdmin = false;
        Assert.Equal(UserUpdateOutcome.Forbidden, (await Service.ExecuteAsync(admin, user.Id, Input, default)).Outcome);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData((UserRole)99)]
    public async Task CannotPromoteOperationalUserToAdminOrUnknownRole(UserRole role)
    {
        Assert.Equal(UserUpdateOutcome.Invalid, (await Service.ExecuteAsync(admin, user.Id, Input with { Role = role }, default)).Outcome);
        Assert.Null(repository.Audit);
    }

    [Fact]
    public async Task SharedAccountCannotHaveItsGlobalCredentialsChangedByTenantAdmin()
    {
        repository.Shared = true;
        Assert.Equal(UserUpdateOutcome.Conflict, (await Service.ExecuteAsync(admin, user.Id, Input, default)).Outcome);
        Assert.Equal("Collector", user.DisplayName); Assert.Null(repository.Audit);
    }

    [Fact]
    public async Task SharedAccountCanChangeOnlyLocalMembershipAccess()
    {
        repository.Shared = true;
        var input = Input with { DisplayName = user.DisplayName, PhoneNumber = user.PhoneNumber!, IsActive = false };
        Assert.Equal(UserUpdateOutcome.Success, (await Service.ExecuteAsync(admin, user.Id, input, default)).Outcome);
        Assert.False(membership.IsActive); Assert.True(repository.RevokeTenant); Assert.False(repository.RevokeAll);
    }

    [Fact]
    public async Task AssignedUserMustBeReassignedBeforeChangingRoleOrDisabling()
    {
        repository.Assigned = true;
        Assert.Equal(UserUpdateOutcome.Conflict, (await Service.ExecuteAsync(admin, user.Id, Input with { Role = UserRole.Supervisor }, default)).Outcome);
        Assert.Equal(UserUpdateOutcome.Conflict, (await Service.ExecuteAsync(admin, user.Id, Input with { IsActive = false }, default)).Outcome);
        Assert.Null(repository.Audit);
    }

    [Fact]
    public async Task OwnerCannotLoseAdminRoleOrAccessEvenWhenEditingSelf()
    {
        var owner = OrganizationMembership.CreateOwner(user.Id, admin.OrganizationId, DateTimeOffset.UtcNow);
        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.User))!.SetValue(owner, user);
        repository.Member = owner;
        var self = admin with { UserId = user.Id };
        Assert.Equal(UserUpdateOutcome.Invalid, (await Service.ExecuteAsync(self, user.Id, Input, default)).Outcome);
        Assert.Equal(UserUpdateOutcome.Invalid, (await Service.ExecuteAsync(self, user.Id, Input with { Role = UserRole.Admin, IsActive = false }, default)).Outcome);
        Assert.True(owner.IsOwner); Assert.True(owner.IsActive);
    }

    [Fact]
    public async Task DuplicateUsernameIsAConflict()
    {
        repository.Saves = false;
        Assert.Equal(UserUpdateOutcome.Conflict, (await Service.ExecuteAsync(admin, user.Id, Input, default)).Outcome);
    }

    private sealed class Passwords : IPasswordCredentialService
    {
        public int Calls { get; private set; }
        public string Hash(string password) { Calls++; return "new-hash"; }
        public bool Verify(ApplicationUser user, string password) => false;
    }
    private sealed class Repository : IUserEditingRepository, IUserManagementRepository
    {
        public OrganizationMembership? Member { get; set; }
        public bool ActiveAdmin { get; set; } = true;
        public bool Shared { get; set; }
        public bool Assigned { get; set; }
        public bool Saves { get; set; } = true;
        public bool RevokeAll { get; private set; }
        public bool RevokeTenant { get; private set; }
        public AdministrativeAuditRecord? Audit { get; private set; }
        public Task<OrganizationMembership?> FindAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(Member?.OrganizationId == organizationId && Member.UserId == userId ? Member : null);
        public Task<bool> IsActiveAdminAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(ActiveAdmin);
        public Task<bool> HasOtherMembershipsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(Shared);
        public Task<bool> HasActiveRouteAssignmentsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(Assigned);
        public Task<bool> SaveAsync(OrganizationMembership membership, AdministrativeAuditRecord audit, bool revokeAllSessions, bool revokeTenantSessions, CancellationToken cancellationToken)
        { Audit = audit; RevokeAll = revokeAllSessions; RevokeTenant = revokeTenantSessions; return Task.FromResult(Saves); }
        public Task<IReadOnlyCollection<OrganizationUser>> ListAsync(Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryCreateAsync(ApplicationUser user, OrganizationMembership membership, AdministrativeAuditRecord audit, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
