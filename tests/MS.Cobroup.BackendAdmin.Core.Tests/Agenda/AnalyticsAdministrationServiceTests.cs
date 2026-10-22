using MS.Cobroup.BackendAdmin.Core.Agenda;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using Xunit;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Agenda;

public sealed class AnalyticsAdministrationServiceTests
{
    [Fact]
    public async Task Admin_and_Supervisor_can_query_overview_routes_and_aging()
    {
        var directory = new FakeAgendaDirectory();
        var service = new AnalyticsAdministrationService(directory);

        var admin = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);

        var overviewResult = await service.GetOverviewAsync(admin, "token-adm", null, null, default);
        var routesResult = await service.GetRoutesPerformanceAsync(admin, "token-adm", null, default);
        var agingResult = await service.GetAgingReportAsync(admin, "token-adm", null, null, default);

        Assert.Equal(200, overviewResult.StatusCode);
        Assert.Equal(200, routesResult.StatusCode);
        Assert.Equal(200, agingResult.StatusCode);
        Assert.Equal("token-adm", directory.LastToken);
    }

    [Fact]
    public async Task Collector_is_forbidden_from_analytics_endpoints()
    {
        var directory = new FakeAgendaDirectory();
        var service = new AnalyticsAdministrationService(directory);

        var collector = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Collector);

        var overviewResult = await service.GetOverviewAsync(collector, "token-col", null, null, default);
        var routesResult = await service.GetRoutesPerformanceAsync(collector, "token-col", null, default);
        var agingResult = await service.GetAgingReportAsync(collector, "token-col", null, null, default);

        Assert.Equal(403, overviewResult.StatusCode);
        Assert.Equal(403, routesResult.StatusCode);
        Assert.Equal(403, agingResult.StatusCode);
        Assert.Null(directory.LastToken);
    }
}
