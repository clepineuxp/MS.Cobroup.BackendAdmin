using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

namespace MS.Cobroup.BackendAdmin.Core.Services;

public sealed class AdministrativeScopeAuthorizer : IAdministrativeScopeAuthorizer
{
    public bool IsInTenant(AuthenticatedActor actor, Guid tenantId) =>
        actor.TenantId != Guid.Empty && actor.TenantId == tenantId;

    public bool CanAdministerTenant(AuthenticatedActor actor, Guid tenantId) =>
        IsInTenant(actor, tenantId) && actor.Roles.Contains(UserRole.Admin);
}
