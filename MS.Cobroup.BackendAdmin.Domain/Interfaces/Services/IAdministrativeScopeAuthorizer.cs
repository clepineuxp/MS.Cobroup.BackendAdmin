using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

public interface IAdministrativeScopeAuthorizer
{
    bool IsInTenant(AuthenticatedActor actor, Guid tenantId);

    bool CanAdministerTenant(AuthenticatedActor actor, Guid tenantId);
}
