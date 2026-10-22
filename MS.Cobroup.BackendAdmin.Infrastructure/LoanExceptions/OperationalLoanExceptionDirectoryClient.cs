using System.Net.Http.Headers;
using System.Text;
using MS.Cobroup.BackendAdmin.Core.LoanExceptions;

namespace MS.Cobroup.BackendAdmin.Infrastructure.LoanExceptions;

public sealed class OperationalLoanExceptionDirectoryClient(HttpClient httpClient) : IOperationalLoanExceptionDirectory
{
    public Task<LoanExceptionProxyResult> SearchAsync(
        string accessToken,
        Guid? routeId,
        Guid? customerId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (routeId.HasValue) query.Add($"routeId={routeId.Value:D}");
        if (customerId.HasValue) query.Add($"customerId={customerId.Value:D}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        return SendAsync(HttpMethod.Get, $"api/operations/v1/loan-exceptions?{string.Join('&', query)}", accessToken, null, cancellationToken);
    }

    public Task<LoanExceptionProxyResult> GetAsync(string accessToken, Guid exceptionId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"api/operations/v1/loan-exceptions/{exceptionId:D}", accessToken, null, cancellationToken);

    public Task<LoanExceptionProxyResult> ApplyDecisionAsync(
        string accessToken,
        Guid exceptionId,
        string json,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, $"api/operations/v1/loan-exceptions/{exceptionId:D}/decision", accessToken, json, cancellationToken);

    private async Task<LoanExceptionProxyResult> SendAsync(
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
            return new LoanExceptionProxyResult(
                (int)response.StatusCode,
                content,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");
        }
        catch (HttpRequestException)
        {
            return LoanExceptionProxyResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LoanExceptionProxyResult.Unavailable();
        }
    }
}
