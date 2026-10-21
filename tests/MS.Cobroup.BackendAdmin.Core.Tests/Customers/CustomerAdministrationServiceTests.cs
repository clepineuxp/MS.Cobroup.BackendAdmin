using MS.Cobroup.BackendAdmin.Core.Customers;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Customers;

public sealed class CustomerAdministrationServiceTests
{
    [Fact]
    public async Task Admin_requests_are_forwarded_to_the_operational_owner()
    {
        var directory = new FakeDirectory();
        var actor = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);

        var result = await new CustomerAdministrationService(directory)
            .SearchAsync(actor, "token", null, "Ana", 1, 25, default);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("token", directory.Token);
    }

    [Fact]
    public async Task Non_admin_requests_are_rejected_in_core()
    {
        var directory = new FakeDirectory();
        var actor = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Supervisor);

        var result = await new CustomerAdministrationService(directory)
            .GetAsync(actor, "token", Guid.NewGuid(), default);

        Assert.Equal(403, result.StatusCode);
        Assert.Null(directory.Token);
    }

    private sealed class FakeDirectory : IOperationalCustomerDirectory
    {
        public string? Token { get; private set; }

        public Task<CustomerProxyResult> SearchAsync(string accessToken, Guid? routeId, string? search, int page, int pageSize, CancellationToken cancellationToken)
        {
            Token = accessToken;
            return Task.FromResult(new CustomerProxyResult(200, "{}", "application/json"));
        }

        public Task<CustomerProxyResult> GetAsync(string accessToken, Guid customerId, CancellationToken cancellationToken)
        {
            Token = accessToken;
            return Task.FromResult(new CustomerProxyResult(200, "{}", "application/json"));
        }

        public Task<CustomerProxyResult> CreateAsync(string accessToken, string json, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerProxyResult> UpdateAsync(string accessToken, Guid customerId, string json, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
