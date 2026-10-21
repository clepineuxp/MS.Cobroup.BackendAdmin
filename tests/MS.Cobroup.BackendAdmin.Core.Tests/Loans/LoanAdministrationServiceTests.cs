using MS.Cobroup.BackendAdmin.Core.Loans;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Loans;

public sealed class LoanAdministrationServiceTests
{
    [Fact]
    public async Task Admin_requests_are_forwarded_to_operational_directory()
    {
        var directory = new FakeLoanDirectory();
        var actor = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);

        var service = new LoanAdministrationService(directory);
        var searchResult = await service.SearchAsync(actor, "token-123", null, null, null, 1, 25, default);
        var getResult = await service.GetAsync(actor, "token-123", Guid.NewGuid(), default);
        var listResult = await service.ListByCustomerAsync(actor, "token-123", Guid.NewGuid(), default);

        Assert.Equal(200, searchResult.StatusCode);
        Assert.Equal(200, getResult.StatusCode);
        Assert.Equal(200, listResult.StatusCode);
        Assert.Equal("token-123", directory.Token);
    }

    [Fact]
    public async Task Non_admin_requests_are_rejected_with_forbidden()
    {
        var directory = new FakeLoanDirectory();
        var supervisor = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Supervisor);
        var collector = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Collector);

        var service = new LoanAdministrationService(directory);

        var result1 = await service.SearchAsync(supervisor, "token", null, null, null, 1, 10, default);
        var result2 = await service.GetAsync(collector, "token", Guid.NewGuid(), default);
        var result3 = await service.ListByCustomerAsync(collector, "token", Guid.NewGuid(), default);

        Assert.Equal(403, result1.StatusCode);
        Assert.Equal(403, result2.StatusCode);
        Assert.Equal(403, result3.StatusCode);
        Assert.Null(directory.Token);
    }

    private sealed class FakeLoanDirectory : IOperationalLoanDirectory
    {
        public string? Token { get; private set; }

        public Task<LoanProxyResult> SearchAsync(
            string accessToken,
            Guid? routeId,
            Guid? customerId,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Token = accessToken;
            return Task.FromResult(new LoanProxyResult(200, "{\"items\":[]}", "application/json"));
        }

        public Task<LoanProxyResult> GetAsync(string accessToken, Guid loanId, CancellationToken cancellationToken)
        {
            Token = accessToken;
            return Task.FromResult(new LoanProxyResult(200, "{\"id\":\"" + loanId + "\"}", "application/json"));
        }

        public Task<LoanProxyResult> ListByCustomerAsync(string accessToken, Guid customerId, CancellationToken cancellationToken)
        {
            Token = accessToken;
            return Task.FromResult(new LoanProxyResult(200, "[]", "application/json"));
        }
    }
}
