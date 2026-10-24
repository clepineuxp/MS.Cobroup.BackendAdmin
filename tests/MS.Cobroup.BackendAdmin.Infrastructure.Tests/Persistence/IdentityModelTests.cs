using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.PaymentMethods;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Persistence;

public sealed class IdentityModelTests
{
    [Fact]
    public void TemplateRangesAndAssignmentsArePersistedWithHistoricalVersions()
    {
        using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=model_test").Options);
        foreach (var type in new[] { typeof(MS.Cobroup.BackendAdmin.Domain.LoanTemplates.LoanTemplate),
            typeof(MS.Cobroup.BackendAdmin.Domain.LoanTemplates.LoanTemplateVersion) })
        {
            var entity = context.Model.FindEntityType(type)!;
            Assert.Equal(18, entity.FindProperty("MinPrincipalAmount")!.GetPrecision());
            Assert.Equal(4, entity.FindProperty("MinPrincipalAmount")!.GetScale());
            Assert.Equal("jsonb", entity.FindProperty("RouteIdsJson")!.GetColumnType());
        }
        Assert.Contains("MinPrincipalAmount", context.Database.GenerateCreateScript());
    }

    [Fact]
    public void OwnershipHasFilteredUniqueIndexWithoutRestrictingOrdinaryMemberships()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql("Host=localhost;Database=model_test;Username=model_test");
        using var context = new IdentityDbContext(options.Options);
        var membership = context.Model.FindEntityType(typeof(OrganizationMembership))!;
        var index = Assert.Single(membership.GetIndexes(), item => item.Properties.Select(property => property.Name)
            .SequenceEqual(["OrganizationId", "IsOwner"]));
        Assert.True(index.IsUnique);
        Assert.Equal("\"IsOwner\" = TRUE", index.GetFilter());
        Assert.Contains(membership.GetIndexes(), item => !item.IsUnique && item.Properties.Select(property => property.Name).SequenceEqual(["OrganizationId"]));
        var sql = context.Database.GenerateCreateScript();
        Assert.Contains("WHERE \"IsOwner\" = TRUE", sql);
    }

    [Fact]
    public void UserModel_EnforcesGlobalUsernameAndMembershipUniqueness()
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        builder.UseNpgsql("Host=localhost;Database=model_test;Username=model_test");
        using var context = new IdentityDbContext(builder.Options);
        var user = context.Model.FindEntityType(typeof(ApplicationUser));
        var membership = context.Model.FindEntityType(typeof(OrganizationMembership));

        var uniqueIndex = Assert.Single(user!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ApplicationUser.NormalizedUsername)]));
        var membershipKey = membership!.FindPrimaryKey();

        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal(
            [nameof(OrganizationMembership.UserId), nameof(OrganizationMembership.OrganizationId)],
            membershipKey!.Properties.Select(property => property.Name));
    }

    [Fact]
    public void RouteModel_EnforcesTenantNameAndCollectorAssignmentUniqueness()
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        builder.UseNpgsql("Host=localhost;Database=model_test;Username=model_test");
        using var context = new IdentityDbContext(builder.Options);
        var route = context.Model.FindEntityType(typeof(CollectionRoute));
        var assignment = context.Model.FindEntityType(typeof(RouteCollectorAssignment));

        var uniqueIndex = Assert.Single(route!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CollectionRoute.OrganizationId), nameof(CollectionRoute.NormalizedName)]));
        var primaryKey = assignment!.FindPrimaryKey();

        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal(
            [nameof(RouteCollectorAssignment.RouteId), nameof(RouteCollectorAssignment.CollectorUserId)],
            primaryKey!.Properties.Select(property => property.Name));
    }

    [Fact]
    public void PaymentMethodModel_EnforcesTenantAndNormalizedNameUniqueness()
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        builder.UseNpgsql("Host=localhost;Database=model_test;Username=model_test");
        using var context = new IdentityDbContext(builder.Options);
        var paymentMethod = context.Model.FindEntityType(typeof(PaymentMethod));

        var uniqueIndex = Assert.Single(paymentMethod!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PaymentMethod.OrganizationId), nameof(PaymentMethod.NormalizedName)]));

        Assert.True(uniqueIndex.IsUnique);
    }

    [Fact]
    public void RefreshTokenModel_EnforcesTokenHashUniqueness()
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        builder.UseNpgsql("Host=localhost;Database=model_test;Username=model_test");
        using var context = new IdentityDbContext(builder.Options);
        var tokenEntity = context.Model.FindEntityType(typeof(RefreshToken));

        Assert.NotNull(tokenEntity);
        var uniqueIndex = Assert.Single(tokenEntity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(RefreshToken.TokenHash)]));

        Assert.True(uniqueIndex.IsUnique);
    }
}
