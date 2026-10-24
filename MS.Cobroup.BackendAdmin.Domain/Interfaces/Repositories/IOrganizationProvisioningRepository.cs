using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

public interface IOrganizationProvisioningRepository
{
    Task<bool> TryCreateAsync(Organization organization, ApplicationUser owner,
        OrganizationMembership membership, AdministrativeAuditRecord audit, CancellationToken cancellationToken);
}
