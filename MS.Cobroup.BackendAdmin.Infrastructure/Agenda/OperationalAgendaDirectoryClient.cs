using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MS.Cobroup.BackendAdmin.Core.Agenda;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Agenda;

public sealed class OperationalAgendaDirectoryClient(HttpClient httpClient) : IOperationalAgendaDirectory
{
    public Task<AgendaProxyResult> GetAgendaAsync(
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        decimal? originLat,
        decimal? originLon,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (routeId.HasValue) query.Add($"routeId={routeId.Value:D}");
        if (targetDate.HasValue) query.Add($"targetDate={targetDate.Value:yyyy-MM-dd}");
        if (originLat.HasValue) query.Add($"originLat={originLat.Value}");
        if (originLon.HasValue) query.Add($"originLon={originLon.Value}");

        var queryString = query.Count > 0 ? "?" + string.Join('&', query) : string.Empty;
        return SendAsync(HttpMethod.Get, $"api/operations/v1/agenda{queryString}", null, accessToken, cancellationToken);
    }

    public Task<AgendaProxyResult> GetMapLocationsAsync(
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (routeId.HasValue) query.Add($"routeId={routeId.Value:D}");
        if (targetDate.HasValue) query.Add($"targetDate={targetDate.Value:yyyy-MM-dd}");

        var queryString = query.Count > 0 ? "?" + string.Join('&', query) : string.Empty;
        return SendAsync(HttpMethod.Get, $"api/operations/v1/agenda/map-locations{queryString}", null, accessToken, cancellationToken);
    }

    public Task<AgendaProxyResult> GetRouteAnalyticsAsync(
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (routeId.HasValue) query.Add($"routeId={routeId.Value:D}");
        if (targetDate.HasValue) query.Add($"targetDate={targetDate.Value:yyyy-MM-dd}");

        var queryString = query.Count > 0 ? "?" + string.Join('&', query) : string.Empty;
        return SendAsync(HttpMethod.Get, $"api/operations/v1/agenda/route-analytics{queryString}", null, accessToken, cancellationToken);
    }

    public Task<AgendaProxyResult> OptimizeSequenceAsync(
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        decimal originLat,
        decimal originLon,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            routeId,
            targetDate,
            originLatitude = originLat,
            originLongitude = originLon
        });

        return SendAsync(HttpMethod.Post, "api/operations/v1/agenda/optimize-sequence", body, accessToken, cancellationToken);
    }

    private async Task<AgendaProxyResult> SendAsync(
        HttpMethod method,
        string uri,
        string? jsonBody,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var content = response.Content.Headers.ContentLength is 0
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken);

            return new AgendaProxyResult(
                (int)response.StatusCode,
                content,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");
        }
        catch (HttpRequestException)
        {
            return AgendaProxyResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AgendaProxyResult.Unavailable();
        }
    }
}
