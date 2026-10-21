using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Persistence;

public sealed class IdentityModelTests
{
    [Fact]
    public void UserModel_EnforcesOrganizationUsernameUniqueness()
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        builder.UseNpgsql("Host=localhost;Database=model_test;Username=model_test");
        builder.UseOpenIddict<Guid>();
        using var context = new IdentityDbContext(builder.Options);
        var user = context.Model.FindEntityType(typeof(ApplicationUser));

        var uniqueIndex = Assert.Single(user!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ApplicationUser.OrganizationId), nameof(ApplicationUser.NormalizedUsername)]));

        Assert.True(uniqueIndex.IsUnique);
    }
}
