using System.Net.Http.Headers;
using MS.Cobroup.BackendAdmin.Core.Loans;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Loans;

public sealed class OperationalLoanDirectoryClient(HttpClient httpClient) : IOperationalLoanDirectory
{
    public Task<LoanProxyResult> SearchAsync(
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
        return SendAsync(HttpMethod.Get, $"api/operations/v1/loans?{string.Join('&', query)}", accessToken, cancellationToken);
    }

    public Task<LoanProxyResult> GetAsync(string accessToken, Guid loanId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"api/operations/v1/loans/{loanId:D}", accessToken, cancellationToken);

    public Task<LoanProxyResult> ListByCustomerAsync(string accessToken, Guid customerId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"api/operations/v1/loans/by-customer/{customerId:D}", accessToken, cancellationToken);

    private async Task<LoanProxyResult> SendAsync(
        HttpMethod method,
        string uri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var content = response.Content.Headers.ContentLength is 0
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken);
            return new LoanProxyResult(
                (int)response.StatusCode,
                content,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");
        }
        catch (HttpRequestException)
        {
            return LoanProxyResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LoanProxyResult.Unavailable();
        }
    }
}
