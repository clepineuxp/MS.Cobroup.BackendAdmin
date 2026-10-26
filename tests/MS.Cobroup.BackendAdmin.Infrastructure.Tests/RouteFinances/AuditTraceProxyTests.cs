using System.Net;
using MS.Cobroup.BackendAdmin.Infrastructure.RouteFinances;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.RouteFinances;

public sealed class AuditTraceProxyTests
{
    [Fact]
    public async Task ForwardsRouteDatesAndBearerToOperationsAndPreservesResponse()
    {
        var routeId = Guid.NewGuid();
        var from = DateTimeOffset.Parse("2026-10-26T00:00:00-05:00");
        using var handler = new CaptureHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://operations.test/") };
        var directory = new OperationalRouteFinanceDirectoryClient(client);
        var result = await directory.ListAuditTraceAsync("test-token", routeId, from, from.AddDays(1), CancellationToken.None);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("[]", result.Content);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Contains($"routeId={routeId:D}", handler.Url);
        Assert.Contains("fromUtc=" + Uri.EscapeDataString(from.ToString("O")), handler.Url);
        Assert.Contains("toUtc=" + Uri.EscapeDataString(from.AddDays(1).ToString("O")), handler.Url);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string Url { get; private set; } = "";
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        }
    }
}
