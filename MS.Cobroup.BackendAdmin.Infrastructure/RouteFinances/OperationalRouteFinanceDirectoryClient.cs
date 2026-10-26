using System.Net.Http.Headers;
using System.Text;
using MS.Cobroup.BackendAdmin.Core.RouteFinances;

namespace MS.Cobroup.BackendAdmin.Infrastructure.RouteFinances;

public sealed class OperationalRouteFinanceDirectoryClient(HttpClient httpClient) : IOperationalRouteFinanceDirectory
{
    public Task<RouteFinanceProxyResult> GetBalanceAsync(
        string accessToken, Guid routeId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"api/operations/v1/route-finances/{routeId:D}/balance", accessToken, null, cancellationToken);

    public Task<RouteFinanceProxyResult> ListEntriesAsync(
        string accessToken, Guid routeId, string section, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get,
            $"api/operations/v1/route-finances/entries?routeId={routeId:D}&section={Uri.EscapeDataString(section)}",
            accessToken, null, cancellationToken);

    public Task<RouteFinanceProxyResult> CreateEntryAsync(
        string accessToken, string json, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "api/operations/v1/route-finances/entries", accessToken, json, cancellationToken);

    public Task<RouteFinanceProxyResult> UpdateEntryAsync(
        string accessToken, Guid entryId, string json, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, $"api/operations/v1/route-finances/entries/{entryId:D}", accessToken, json, cancellationToken);

    private async Task<RouteFinanceProxyResult> SendAsync(
        HttpMethod method,
        string uri,
        string accessToken,
        string? json,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var content = response.Content.Headers.ContentLength is 0
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken);
            return new RouteFinanceProxyResult(
                (int)response.StatusCode,
                content,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");
        }
        catch (HttpRequestException)
        {
            return RouteFinanceProxyResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RouteFinanceProxyResult.Unavailable();
        }
    }
}
