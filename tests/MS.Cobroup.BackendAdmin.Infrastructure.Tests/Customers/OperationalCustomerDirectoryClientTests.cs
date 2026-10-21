using System.Net;
using MS.Cobroup.BackendAdmin.Infrastructure.Customers;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Customers;

public sealed class OperationalCustomerDirectoryClientTests
{
    [Fact]
    public async Task Search_forwards_bearer_and_query_to_operational_backend()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"items\":[]}")
        });
        var client = new OperationalCustomerDirectoryClient(new HttpClient(handler) { BaseAddress = new Uri("https://operations.local/") });

        var result = await client.SearchAsync("access-token", Guid.Parse("11111111-1111-1111-1111-111111111111"), "Ana Pérez", 2, 10, default);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("access-token", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("page=2", handler.Request.RequestUri!.Query);
        Assert.Contains("Ana%20P%C3%A9rez", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task Network_failure_is_reported_as_unavailable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        var client = new OperationalCustomerDirectoryClient(new HttpClient(handler) { BaseAddress = new Uri("https://operations.local/") });

        var result = await client.GetAsync("token", Guid.NewGuid(), default);

        Assert.Equal(503, result.StatusCode);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(response(request));
        }
    }
}
