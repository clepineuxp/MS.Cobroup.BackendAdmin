using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using Xunit;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Persistence;

public sealed class PostgresRefreshTokenConcurrencyTests
{
    private readonly string connectionString;

    public PostgresRefreshTokenConcurrencyTests()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Api.Security.SymmetricKeyService>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        connectionString = config["ConnectionStrings:IdentityDatabase"]
            ?? throw new InvalidOperationException("ConnectionStrings:IdentityDatabase is required for PostgreSQL concurrency tests.");
    }

    [Fact]
    public async Task ConcurrentRotation_OnRealPostgreSql_AllowsExactlyOneSuccessAndRevokesOnReplay()
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        await using var setupContext = new IdentityDbContext(optionsBuilder.Options);
        var canConnect = await setupContext.Database.CanConnectAsync();
        if (!canConnect)
        {
            throw new InvalidOperationException($"Cannot connect to PostgreSQL database via '{connectionString}'. Database must be accessible.");
        }

        // Setup test data in dev_cobroup_identity
        var org = Organization.Create(
            $"test-conc-{Guid.NewGuid():N}"[..16],
            "Concurrency Test Org",
            DateTimeOffset.UtcNow);

        var user = ApplicationUser.Create(
            $"test-conc-user-{Guid.NewGuid():N}"[..20],
            "Concurrency Test User",
            "dummy_hash",
            DateTimeOffset.UtcNow);

        var membership = OrganizationMembership.Create(
            user.Id,
            org.Id,
            UserRole.Admin,
            DateTimeOffset.UtcNow);

        setupContext.Organizations.Add(org);
        setupContext.Users.Add(user);
        setupContext.OrganizationMemberships.Add(membership);

        var familyId = Guid.NewGuid();
        var initialRawToken = $"initial-token-{Guid.NewGuid():N}";
        var initialTokenHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(initialRawToken)));
        var now = DateTimeOffset.UtcNow;

        var initialToken = new RefreshToken(
            familyId,
            user.Id,
            org.Id,
            initialTokenHash,
            now.AddDays(30),
            now);

        setupContext.RefreshTokens.Add(initialToken);
        await setupContext.SaveChangesAsync();

        try
        {
            // Launch 2 concurrent rotations using synchronization barrier
            using var barrier = new Barrier(2);

            var newRawToken1 = $"new-token-1-{Guid.NewGuid():N}";
            var newTokenHash1 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(newRawToken1)));
            var newToken1 = new RefreshToken(familyId, user.Id, org.Id, newTokenHash1, now.AddDays(30), now);

            var newRawToken2 = $"new-token-2-{Guid.NewGuid():N}";
            var newTokenHash2 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(newRawToken2)));
            var newToken2 = new RefreshToken(familyId, user.Id, org.Id, newTokenHash2, now.AddDays(30), now);

            var task1 = Task.Run(async () =>
            {
                await using var db1 = new IdentityDbContext(optionsBuilder.Options);
                var repo1 = new EfIdentityRepository(db1);
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                return await repo1.RotateRefreshTokenAtomicAsync(initialTokenHash, newToken1, DateTimeOffset.UtcNow, CancellationToken.None);
            });

            var task2 = Task.Run(async () =>
            {
                await using var db2 = new IdentityDbContext(optionsBuilder.Options);
                var repo2 = new EfIdentityRepository(db2);
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                return await repo2.RotateRefreshTokenAtomicAsync(initialTokenHash, newToken2, DateTimeOffset.UtcNow, CancellationToken.None);
            });

            var results = await Task.WhenAll(task1, task2);

            // B-05: Exactly one rotation must succeed and one must detect replay!
            var successes = results.Count(r => r);
            var failures = results.Count(r => !r);

            Assert.Equal(1, successes);
            Assert.Equal(1, failures);

            // Verify PostgreSQL database state:
            await using var verifyContext = new IdentityDbContext(optionsBuilder.Options);
            var reloadedInitial = await verifyContext.RefreshTokens.SingleAsync(t => t.TokenHash == initialTokenHash);
            
            // Initial token must be marked inactive, revoked and replaced
            Assert.False(reloadedInitial.IsActive);
            Assert.NotNull(reloadedInitial.RevokedAtUtc);
            Assert.NotNull(reloadedInitial.ReplacedByTokenHash);

            // Query all tokens in this family
            var allFamilyTokens = await verifyContext.RefreshTokens
                .Where(t => t.FamilyId == familyId)
                .ToListAsync();

            // Exactly 2 tokens must exist: the parent token and the single winning rotated token
            Assert.Equal(2, allFamilyTokens.Count);

            // Uniqueness of TokenHash across family tokens
            var distinctHashes = allFamilyTokens.Select(t => t.TokenHash).Distinct().Count();
            Assert.Equal(allFamilyTokens.Count, distinctHashes);

            // Because the second concurrent thread detected replay of the consumed parent token,
            // the entire family was revoked. Zero tokens in the family must remain active!
            Assert.All(allFamilyTokens, t => Assert.False(t.IsActive));
            Assert.All(allFamilyTokens, t => Assert.NotNull(t.RevokedAtUtc));
        }
        finally
        {
            // Cleanup test data
            await using var cleanupContext = new IdentityDbContext(optionsBuilder.Options);
            var cleanupTokens = await cleanupContext.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync();
            cleanupContext.RefreshTokens.RemoveRange(cleanupTokens);
            cleanupContext.OrganizationMemberships.Remove(membership);
            cleanupContext.Users.Remove(user);
            cleanupContext.Organizations.Remove(org);
            await cleanupContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ConcurrentOrganizationSwitch_OnRealPostgreSql_AllowsExactlyOneSuccess_AndRevokesPreviousFamily()
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        await using var setupContext = new IdentityDbContext(optionsBuilder.Options);
        var canConnect = await setupContext.Database.CanConnectAsync();
        if (!canConnect)
        {
            throw new InvalidOperationException($"Cannot connect to PostgreSQL database via '{connectionString}'.");
        }

        var org1 = Organization.Create($"t-sw1-{Guid.NewGuid():N}"[..16], "Switch Org 1", DateTimeOffset.UtcNow);
        var org2 = Organization.Create($"t-sw2-{Guid.NewGuid():N}"[..16], "Switch Org 2", DateTimeOffset.UtcNow);
        var user = ApplicationUser.Create($"t-sw-u-{Guid.NewGuid():N}"[..20], "Switch User", "dummy_hash", DateTimeOffset.UtcNow);
        var m1 = OrganizationMembership.Create(user.Id, org1.Id, UserRole.Admin, DateTimeOffset.UtcNow);
        var m2 = OrganizationMembership.Create(user.Id, org2.Id, UserRole.Supervisor, DateTimeOffset.UtcNow);

        setupContext.Organizations.AddRange(org1, org2);
        setupContext.Users.Add(user);
        setupContext.OrganizationMemberships.AddRange(m1, m2);

        var oldFamilyId = Guid.NewGuid();
        var initialRawToken = $"initial-switch-{Guid.NewGuid():N}";
        var initialTokenHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(initialRawToken)));
        var now = DateTimeOffset.UtcNow;

        var initialToken = new RefreshToken(
            oldFamilyId,
            user.Id,
            org1.Id,
            initialTokenHash,
            now.AddDays(30),
            now);

        setupContext.RefreshTokens.Add(initialToken);
        await setupContext.SaveChangesAsync();

        var newFamily1 = Guid.NewGuid();
        var newFamily2 = Guid.NewGuid();

        try
        {
            using var barrier = new Barrier(2);

            var newTokenHash1 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"tok1-{Guid.NewGuid():N}")));
            var newToken1 = new RefreshToken(newFamily1, user.Id, org2.Id, newTokenHash1, now.AddDays(30), now);

            var newTokenHash2 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"tok2-{Guid.NewGuid():N}")));
            var newToken2 = new RefreshToken(newFamily2, user.Id, org2.Id, newTokenHash2, now.AddDays(30), now);

            var task1 = Task.Run(async () =>
            {
                await using var db1 = new IdentityDbContext(optionsBuilder.Options);
                var repo1 = new EfIdentityRepository(db1);
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                return await repo1.SwitchOrganizationSessionAtomicAsync(
                    initialTokenHash,
                    user.Id,
                    org1.Id,
                    newToken1,
                    DateTimeOffset.UtcNow,
                    "Switch to org2 (1)",
                    CancellationToken.None);
            });

            var task2 = Task.Run(async () =>
            {
                await using var db2 = new IdentityDbContext(optionsBuilder.Options);
                var repo2 = new EfIdentityRepository(db2);
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                return await repo2.SwitchOrganizationSessionAtomicAsync(
                    initialTokenHash,
                    user.Id,
                    org1.Id,
                    newToken2,
                    DateTimeOffset.UtcNow,
                    "Switch to org2 (2)",
                    CancellationToken.None);
            });

            var results = await Task.WhenAll(task1, task2);

            // B-17: Exactly ONE switch must succeed!
            var successes = results.Count(r => r);
            var failures = results.Count(r => !r);

            Assert.Equal(1, successes);
            Assert.Equal(1, failures);

            // Verify PostgreSQL state
            await using var verifyContext = new IdentityDbContext(optionsBuilder.Options);
            var oldTokens = await verifyContext.RefreshTokens.Where(t => t.FamilyId == oldFamilyId).ToListAsync();
            Assert.All(oldTokens, t => Assert.False(t.IsActive));
            Assert.All(oldTokens, t => Assert.NotNull(t.RevokedAtUtc));

            // Verify that only the winning family's initial token was created and is active
            var activeTokens = await verifyContext.RefreshTokens
                .Where(t => (t.FamilyId == newFamily1 || t.FamilyId == newFamily2) && t.RevokedAtUtc == null)
                .ToListAsync();

            Assert.Single(activeTokens);
        }
        finally
        {
            await using var cleanupContext = new IdentityDbContext(optionsBuilder.Options);
            var cleanupTokens = await cleanupContext.RefreshTokens
                .Where(t => t.FamilyId == oldFamilyId || t.FamilyId == newFamily1 || t.FamilyId == newFamily2)
                .ToListAsync();
            cleanupContext.RefreshTokens.RemoveRange(cleanupTokens);
            cleanupContext.OrganizationMemberships.RemoveRange(m1, m2);
            cleanupContext.Users.Remove(user);
            cleanupContext.Organizations.RemoveRange(org1, org2);
            await cleanupContext.SaveChangesAsync();
        }
    }
}
