using System.Net.Http.Headers;
using System.Text;
using MS.Cobroup.BackendAdmin.Core.Customers;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Customers;

public sealed class OperationalCustomerDirectoryClient(HttpClient httpClient) : IOperationalCustomerDirectory
{
    public Task<CustomerProxyResult> SearchAsync(
        string accessToken,
        Guid? routeId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (routeId.HasValue) query.Add($"routeId={routeId.Value:D}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        return SendAsync(HttpMethod.Get, $"api/operations/v1/customers?{string.Join('&', query)}", accessToken, null, cancellationToken);
    }

    public Task<CustomerProxyResult> GetAsync(string accessToken, Guid customerId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"api/operations/v1/customers/{customerId:D}", accessToken, null, cancellationToken);

    public Task<CustomerProxyResult> CreateAsync(string accessToken, string json, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "api/operations/v1/customers", accessToken, json, cancellationToken);

    public Task<CustomerProxyResult> UpdateAsync(
        string accessToken,
        Guid customerId,
        string json,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, $"api/operations/v1/customers/{customerId:D}", accessToken, json, cancellationToken);

    private async Task<CustomerProxyResult> SendAsync(
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
            return new CustomerProxyResult(
                (int)response.StatusCode,
                content,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");
        }
        catch (HttpRequestException)
        {
            return CustomerProxyResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CustomerProxyResult.Unavailable();
        }
    }
}
