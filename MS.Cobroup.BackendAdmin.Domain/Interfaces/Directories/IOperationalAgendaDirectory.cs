namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Directories;

public interface IOperationalAgendaDirectory
{
    Task<AgendaProxyResult> GetAgendaAsync(
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        decimal? originLat,
        decimal? originLon,
        CancellationToken cancellationToken);

    Task<AgendaProxyResult> GetMapLocationsAsync(
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        CancellationToken cancellationToken);

    Task<AgendaProxyResult> GetRouteAnalyticsAsync(
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        CancellationToken cancellationToken);

    Task<AgendaProxyResult> OptimizeSequenceAsync(
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        decimal originLat,
        decimal originLon,
        CancellationToken cancellationToken);
}

public sealed record AgendaProxyResult(int StatusCode, string? Content, string ContentType)
{
    public static AgendaProxyResult Unavailable() =>
        new(503, "{\"title\":\"Servicio operativo no disponible\",\"status\":503}", "application/problem+json");

    public static AgendaProxyResult Forbidden(string message = "Acceso denegado a los recursos solicitados.") =>
        new(403, $"{{\"title\":\"Acceso denegado\",\"detail\":\"{message}\",\"status\":403}}", "application/problem+json");
}
