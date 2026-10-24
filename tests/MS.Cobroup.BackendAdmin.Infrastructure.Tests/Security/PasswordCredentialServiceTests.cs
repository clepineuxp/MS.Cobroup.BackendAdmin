using Microsoft.AspNetCore.Identity;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Infrastructure.Security;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

public sealed class PasswordCredentialServiceTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("short")]
    [InlineData("  ")]
    public void HashAcceptsNonEmptyPasswordsWithoutLengthOrComplexityRequirements(string password)
    {
        var service = new AspNetPasswordCredentialService(new PasswordHasher<object>());
        var hash = service.Hash(password);
        var user = ApplicationUser.Create("test", "Test", hash, DateTimeOffset.UtcNow);
        Assert.NotEqual(password, hash);
        Assert.True(service.Verify(user, password));
        Assert.False(service.Verify(user, password + "wrong"));
    }

    [Fact]
    public void EmptyPasswordIsRejected()
    {
        var service = new AspNetPasswordCredentialService(new PasswordHasher<object>());
        Assert.Throws<ArgumentException>(() => service.Hash(""));
    }
}
