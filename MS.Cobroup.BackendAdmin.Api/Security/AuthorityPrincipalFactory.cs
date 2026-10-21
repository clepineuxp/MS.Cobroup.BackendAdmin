using System.Security.Claims;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using OpenIddict.Abstractions;

namespace MS.Cobroup.BackendAdmin.Api.Security;

public static class AuthorityPrincipalFactory
{
    public static ClaimsPrincipal Create(
        ApplicationUser user,
        OrganizationMembership? membership = null)
    {
        var claims = new List<Claim>
        {
            new(OpenIddictConstants.Claims.Subject, user.Id.ToString()),
            new(OpenIddictConstants.Claims.Name, user.DisplayName),
            new(OpenIddictConstants.Claims.PreferredUsername, user.Username)
        };
        if (membership is null)
        {
            claims.Add(new Claim("organization_membership_required", bool.TrueString));
        }
        else
        {
            claims.Add(new Claim(OpenIddictConstants.Claims.Role, membership.Role.ToString()));
            claims.Add(new Claim("organization_id", membership.OrganizationId.ToString()));
            claims.Add(new Claim("organization_code", membership.Organization.Code));
        }

        var identity = new ClaimsIdentity(
            claims,
            AuthenticationSchemes.AuthorityCookie,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        return new ClaimsPrincipal(identity);
    }
}
