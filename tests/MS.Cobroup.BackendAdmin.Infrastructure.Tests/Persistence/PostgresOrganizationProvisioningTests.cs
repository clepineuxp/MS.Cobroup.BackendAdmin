using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Core.Users;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using MS.Cobroup.BackendAdmin.Infrastructure.Security;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Persistence;

[Trait("Category", "PostgreSql")]
public sealed class PostgresOrganizationProvisioningTests : IAsyncLifetime
{
    private readonly string prefix = $"provision-{Guid.NewGuid():N}";
    private readonly DbContextOptions<IdentityDbContext> options;
    private readonly AspNetPasswordCredentialService passwords = new(new PasswordHasher<object>());
    private const string Password = "private-integration-password";

    public PostgresOrganizationProvisioningTests()
    {
        // Deliberately no User Secrets fallback: these tests may only target an explicit test database.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__IdentityDatabase to an isolated PostgreSQL test database.");
        var database = new NpgsqlConnectionStringBuilder(connection).Database ?? "";
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provisioning integration tests require an explicitly named test database.");
        options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var db = new IdentityDbContext(options);
        var organizationIds = await db.Organizations.Where(item => item.Code.StartsWith(prefix.ToUpper())).Select(item => item.Id).ToArrayAsync();
        await db.AdministrativeAuditRecords.Where(item => organizationIds.Contains(item.OrganizationId)).ExecuteDeleteAsync();
        await db.OrganizationMemberships.Where(item => organizationIds.Contains(item.OrganizationId)).ExecuteDeleteAsync();
        await db.Users.Where(item => item.NormalizedUsername.StartsWith(prefix.ToUpper())).ExecuteDeleteAsync();
        await db.Organizations.Where(item => organizationIds.Contains(item.Id)).ExecuteDeleteAsync();
    }

    private ProvisionOrganizationCommand Command(string suffix, string? username = null) =>
        new($"{prefix}-{suffix}", "Integration organization", username ?? $"{prefix}-{suffix}", "Owner", Password, "+573001234567");

    private async Task<ProvisionOrganizationResult> Provision(ProvisionOrganizationCommand command)
    {
        await using var db = new IdentityDbContext(options);
        return await new ProvisionOrganizationService(new EfOrganizationProvisioningRepository(db), passwords).ExecuteAsync(command, default);
    }

    [Fact]
    public async Task ProvisioningAndTeamCreationPersistOwnerMembershipHashAndAuditTogether()
    {
        var result = await Provision(Command("a"));
        Assert.True(result.Created);
        await using var db = new IdentityDbContext(options);
        var owner = await db.Users.SingleAsync(item => item.Id == result.OwnerUserId);
        Assert.True(passwords.Verify(owner, Password));
        Assert.NotEqual(Password, owner.PasswordHash);
        var membership = await db.OrganizationMemberships.SingleAsync(item => item.OrganizationId == result.OrganizationId);
        Assert.True(membership.IsOwner);
        Assert.Equal(UserRole.Admin, membership.Role);
        Assert.Single(await db.AdministrativeAuditRecords.Where(item => item.OrganizationId == result.OrganizationId).ToArrayAsync());

        var users = new UserManagementService(new EfUserManagementRepository(db), passwords);
        var actor = new TenantActor(owner.Id, membership.OrganizationId, UserRole.Admin);
        var created = await users.CreateAsync(actor, new($"{prefix}-collector", "Collector", Password, UserRole.Collector, "+573001234567"), default);
        Assert.Equal(UserManagementOutcome.Success, created.Outcome);
        var directory = await new EfUserDirectoryRepository(db).ListAssignableAsync(membership.OrganizationId, default);
        Assert.Equal(created.User!.Id, Assert.Single(directory).Id);
        Assert.Equal(2, (await users.ListAsync(actor, default)).Users!.Count);
        var token = new MS.Cobroup.BackendAdmin.Domain.Identity.RefreshToken(Guid.NewGuid(), created.User.Id, membership.OrganizationId,
            Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow, "test-key");
        db.RefreshTokens.Add(token); await db.SaveChangesAsync();
        var updateService = new UpdateOrganizationUserService(new EfUserManagementRepository(db), new EfUserEditingRepository(db), passwords);
        var updated = await updateService.ExecuteAsync(actor, created.User.Id,
            new(created.User.Username, "Updated Collector", "+573001234567", UserRole.Supervisor, true), default);
        Assert.Equal(UserUpdateOutcome.Success, updated.Outcome);
        db.ChangeTracker.Clear();
        Assert.Equal("Updated Collector", (await db.Users.SingleAsync(item => item.Id == created.User.Id)).DisplayName);
        Assert.Equal(UserRole.Supervisor, (await db.OrganizationMemberships.SingleAsync(item => item.UserId == created.User.Id && item.OrganizationId == membership.OrganizationId)).Role);
        Assert.NotNull((await db.RefreshTokens.SingleAsync(item => item.Id == token.Id)).RevokedAtUtc);
        Assert.Contains(await db.AdministrativeAuditRecords.Where(item => item.OrganizationId == membership.OrganizationId).ToArrayAsync(), item => item.EventType == "user.updated");
        var forged = actor with { OrganizationId = Guid.NewGuid() };
        Assert.Equal(UserManagementOutcome.Forbidden, (await users.ListAsync(forged, default)).Outcome);
    }

    [Fact]
    public async Task DuplicateUsernameDuringEditingRollsBackProfileAndAudit()
    {
        var first = await Provision(Command("a")); var second = await Provision(Command("b"));
        await using var db = new IdentityDbContext(options);
        var target = await db.Users.SingleAsync(item => item.Id == second.OwnerUserId);
        var originalName = target.Username;
        var service = new UpdateOrganizationUserService(new EfUserManagementRepository(db), new EfUserEditingRepository(db), passwords);
        var result = await service.ExecuteAsync(new(target.Id, second.OrganizationId!.Value, UserRole.Admin), target.Id,
            new(Command("a").Username, "Changed Owner", "+573001234567", UserRole.Admin, true), default);
        Assert.Equal(UserUpdateOutcome.Conflict, result.Outcome);
        db.ChangeTracker.Clear();
        Assert.Equal(originalName, (await db.Users.SingleAsync(item => item.Id == target.Id)).Username);
        Assert.DoesNotContain(await db.AdministrativeAuditRecords.Where(item => item.OrganizationId == second.OrganizationId).ToArrayAsync(), item => item.EventType == "user.updated");
    }

    [Fact]
    public async Task DuplicateCodeRollsBackNewUserAndDuplicateUsernameRollsBackNewOrganization()
    {
        Assert.True((await Provision(Command("a"))).Created);
        Assert.False((await Provision(Command("a", $"{prefix}-other"))).Created);
        Assert.False((await Provision(Command("b", $"{prefix}-a".ToUpperInvariant()))).Created);
        await using var db = new IdentityDbContext(options);
        Assert.Equal(1, await db.Organizations.CountAsync(item => item.Code.StartsWith(prefix.ToUpper())));
        Assert.Equal(1, await db.Users.CountAsync(item => item.NormalizedUsername.StartsWith(prefix.ToUpper())));
    }

    [Fact]
    public async Task ConcurrentGlobalUsernameCollisionAllowsOnlyOneCompleteOrganization()
    {
        var first = Command("a", $"{prefix}-same");
        var second = Command("b", $"{prefix}-same".ToUpperInvariant());
        var results = await Task.WhenAll(Provision(first), Provision(second));
        Assert.Single(results, item => item.Created);
        await using var db = new IdentityDbContext(options);
        Assert.Equal(1, await db.Organizations.CountAsync(item => item.Code.StartsWith(prefix.ToUpper())));
        Assert.Equal(1, await db.Users.CountAsync(item => item.NormalizedUsername.StartsWith(prefix.ToUpper())));
    }
}
