using System.Security.Claims;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Api.Security;

public static class TenantActorReader
{
    public static bool TryRead(ClaimsPrincipal principal, out TenantActor actor)
    {
        var userIdText = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue("user_id");
        var organizationIdText = principal.FindFirstValue("organization_id");
        var roleText = principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role");

        if (Guid.TryParse(userIdText, out var userId)
            && Guid.TryParse(organizationIdText, out var organizationId)
            && Enum.TryParse<UserRole>(roleText, ignoreCase: true, out var role))
        {
            actor = new TenantActor(userId, organizationId, role);
            return true;
        }

        actor = null!;
        return false;
    }
}
