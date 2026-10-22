using MS.Cobroup.BackendAdmin.Core.Agenda;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using Xunit;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Agenda;

public sealed class MapSupervisionServiceTests
{
    [Fact]
    public async Task Admin_and_Supervisor_can_query_map_locations()
    {
        var directory = new FakeAgendaDirectory();
        var service = new MapSupervisionService(directory);

        var admin = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);
        var supervisor = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Supervisor);

        var adminResult = await service.GetMapLocationsAsync(admin, "admin-token", null, null, default);
        var supResult = await service.GetMapLocationsAsync(supervisor, "sup-token", null, null, default);

        Assert.Equal(200, adminResult.StatusCode);
        Assert.Equal(200, supResult.StatusCode);
        Assert.Equal("sup-token", directory.LastToken);
    }

    [Fact]
    public async Task Collector_is_forbidden_from_administrative_map()
    {
        var directory = new FakeAgendaDirectory();
        var service = new MapSupervisionService(directory);

        var collector = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Collector);
        var result = await service.GetMapLocationsAsync(collector, "collector-token", null, null, default);

        Assert.Equal(403, result.StatusCode);
        Assert.Null(directory.LastToken);
    }
}

internal sealed class FakeAgendaDirectory : IOperationalAgendaDirectory
{
    public string? LastToken { get; private set; }

    public Task<AgendaProxyResult> GetAgendaAsync(string accessToken, Guid? routeId, DateOnly? targetDate, decimal? originLat, decimal? originLon, CancellationToken cancellationToken)
    {
        LastToken = accessToken;
        return Task.FromResult(new AgendaProxyResult(200, "{\"items\":[]}", "application/json"));
    }

    public Task<AgendaProxyResult> GetMapLocationsAsync(string accessToken, Guid? routeId, DateOnly? targetDate, CancellationToken cancellationToken)
    {
        LastToken = accessToken;
        return Task.FromResult(new AgendaProxyResult(200, "[]", "application/json"));
    }

    public Task<AgendaProxyResult> GetRouteAnalyticsAsync(string accessToken, Guid? routeId, DateOnly? targetDate, CancellationToken cancellationToken)
    {
        LastToken = accessToken;
        return Task.FromResult(new AgendaProxyResult(200, "{\"agingBuckets\":[]}", "application/json"));
    }

    public Task<AgendaProxyResult> OptimizeSequenceAsync(string accessToken, Guid? routeId, DateOnly? targetDate, decimal originLat, decimal originLon, CancellationToken cancellationToken)
    {
        LastToken = accessToken;
        return Task.FromResult(new AgendaProxyResult(200, "{\"items\":[]}", "application/json"));
    }
}
