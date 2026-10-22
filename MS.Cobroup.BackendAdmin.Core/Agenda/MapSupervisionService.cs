using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Agenda;

public sealed class MapSupervisionService(IOperationalAgendaDirectory directory)
{
    public Task<AgendaProxyResult> GetMapLocationsAsync(
        TenantActor actor,
        string accessToken,
        Guid? routeId,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not (UserRole.Admin or UserRole.Supervisor))
        {
            return Task.FromResult(AgendaProxyResult.Forbidden("Solo administradores y supervisores pueden consultar el mapa de supervisión."));
        }

        return directory.GetMapLocationsAsync(accessToken, routeId, targetDate, cancellationToken);
    }
}
