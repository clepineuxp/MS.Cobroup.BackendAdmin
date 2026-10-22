using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Agenda;

public sealed class AnalyticsAdministrationService(IOperationalAgendaDirectory directory)
{
    public Task<AgendaProxyResult> GetOverviewAsync(
        TenantActor actor,
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not (UserRole.Admin or UserRole.Supervisor))
        {
            return Task.FromResult(AgendaProxyResult.Forbidden("Solo administradores y supervisores pueden consultar analítica de cartera."));
        }

        return directory.GetRouteAnalyticsAsync(accessToken, routeId, targetDate, cancellationToken);
    }

    public Task<AgendaProxyResult> GetRoutesPerformanceAsync(
        TenantActor actor,
        string accessToken,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not (UserRole.Admin or UserRole.Supervisor))
        {
            return Task.FromResult(AgendaProxyResult.Forbidden("Solo administradores y supervisores pueden consultar el rendimiento de rutas."));
        }

        return directory.GetRouteAnalyticsAsync(accessToken, null, targetDate, cancellationToken);
    }

    public Task<AgendaProxyResult> GetAgingReportAsync(
        TenantActor actor,
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not (UserRole.Admin or UserRole.Supervisor))
        {
            return Task.FromResult(AgendaProxyResult.Forbidden("Solo administradores y supervisores pueden consultar el reporte de envejecimiento de cartera."));
        }

        return directory.GetRouteAnalyticsAsync(accessToken, routeId, targetDate, cancellationToken);
    }
}
