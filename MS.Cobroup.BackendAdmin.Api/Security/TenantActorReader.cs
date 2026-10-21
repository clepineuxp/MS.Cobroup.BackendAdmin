using System.Security.Claims;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using OpenIddict.Abstractions;

namespace MS.Cobroup.BackendAdmin.Api.Security;

public static class TenantActorReader
{
    public static bool TryRead(ClaimsPrincipal principal, out TenantActor actor)
    {
        var userIdText = principal.FindFirstValue(OpenIddictConstants.Claims.Subject);
        var organizationIdText = principal.FindFirstValue("organization_id");
        var roleText = principal.FindFirstValue(OpenIddictConstants.Claims.Role);
        if (Guid.TryParse(userIdText, out var userId)
            && Guid.TryParse(organizationIdText, out var organizationId)
            && Enum.TryParse<UserRole>(roleText, ignoreCase: false, out var role))
        {
            actor = new TenantActor(userId, organizationId, role);
            return true;
        }

        actor = null!;
        return false;
    }
}
