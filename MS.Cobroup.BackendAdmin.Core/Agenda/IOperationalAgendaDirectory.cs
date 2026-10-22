namespace MS.Cobroup.BackendAdmin.Core.Agenda;

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
