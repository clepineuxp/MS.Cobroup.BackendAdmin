using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Persistence;

public sealed class IdentityModelTests
{
    [Fact]
    public void UserModel_EnforcesGlobalUsernameAndMembershipUniqueness()
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        builder.UseNpgsql("Host=localhost;Database=model_test;Username=model_test");
        builder.UseOpenIddict<Guid>();
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
        builder.UseOpenIddict<Guid>();
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
}
