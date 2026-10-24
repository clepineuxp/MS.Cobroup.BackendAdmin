using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MS.Cobroup.BackendAdmin.Api.Provisioning;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

public sealed class OrganizationProvisioningCliTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    public async Task ReadsCredentialsFromStdinAndOutputsOnlyIdentifiers(bool created, int expectedCode)
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOrganizationProvisioningRepository>(new Repository(created))
            .AddSingleton<IPasswordCredentialService, Passwords>()
            .AddScoped<ProvisionOrganizationService>().BuildServiceProvider();
        var json = JsonSerializer.Serialize(new ProvisionOrganizationCommand("NORTE", "Organización Norte", "owner", "Dueño", "private-owner-password", "+573001234567"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await OrganizationProvisioningCli.RunAsync(services, new StringReader(json), output, error);
        Assert.Equal(expectedCode, code);
        Assert.DoesNotContain("private-owner-password", output.ToString() + error);
        Assert.DoesNotContain("hash", output.ToString() + error);
        if (created)
        {
            var result = JsonDocument.Parse(output.ToString());
            Assert.Equal(2, result.RootElement.EnumerateObject().Count());
            Assert.NotEqual(Guid.Empty, result.RootElement.GetProperty("OrganizationId").GetGuid());
        }
    }

    [Fact]
    public async Task MalformedInputDoesNotEchoCredentials()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await OrganizationProvisioningCli.RunAsync(services, new StringReader("{\"password\":\"private-password\","), output, error);
        Assert.Equal(1, code);
        Assert.DoesNotContain("private-password", output.ToString() + error);
    }

    private sealed class Repository(bool created) : IOrganizationProvisioningRepository
    {
        public Task<bool> TryCreateAsync(Organization organization, ApplicationUser owner, OrganizationMembership membership, AdministrativeAuditRecord audit, CancellationToken cancellationToken) => Task.FromResult(created);
    }
    private sealed class Passwords : IPasswordCredentialService
    {
        public string Hash(string password) => "hash";
        public bool Verify(ApplicationUser user, string password) => false;
    }
}
