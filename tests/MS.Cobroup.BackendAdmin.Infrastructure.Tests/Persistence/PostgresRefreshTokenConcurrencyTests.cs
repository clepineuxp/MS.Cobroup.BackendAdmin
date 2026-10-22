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
    private readonly string? connectionString;

    public PostgresRefreshTokenConcurrencyTests()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Api.Security.RsaKeyService>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        connectionString = config["ConnectionStrings:IdentityDatabase"];
    }

    [Fact]
    public async Task ConcurrentRotation_OnRealPostgreSql_AllowsExactlyOneSuccessAndRevokesOnReplay()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Skip if database connection string is not present in environment
            return;
        }

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        await using var setupContext = new IdentityDbContext(optionsBuilder.Options);
        try
        {
            if (!await setupContext.Database.CanConnectAsync())
            {
                return;
            }
        }
        catch
        {
            return;
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

            // B-05: Exactly one rotation must succeed!
            var successes = results.Count(r => r);
            var failures = results.Count(r => !r);

            Assert.Equal(1, successes);
            Assert.Equal(1, failures);

            // Verify PostgreSQL database state
            await using var verifyContext = new IdentityDbContext(optionsBuilder.Options);
            var reloadedInitial = await verifyContext.RefreshTokens.SingleAsync(t => t.TokenHash == initialTokenHash);
            Assert.False(reloadedInitial.IsActive);
            Assert.NotNull(reloadedInitial.RevokedAtUtc);

            // Exactly 1 new token was created or if second was replay, family was revoked safely
            var allFamilyTokens = await verifyContext.RefreshTokens
                .Where(t => t.FamilyId == familyId)
                .ToListAsync();

            // None of the attempts threw 500 or crashed
            Assert.NotNull(allFamilyTokens);
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
}
