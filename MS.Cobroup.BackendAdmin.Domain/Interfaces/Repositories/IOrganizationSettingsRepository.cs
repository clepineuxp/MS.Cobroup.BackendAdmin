using MS.Cobroup.BackendAdmin.Domain.Configuration;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

public interface IOrganizationSettingsRepository
{
    Task<OrganizationSettings?> FindAsync(Guid organizationId, CancellationToken cancellationToken);

    Task SaveAsync(
        OrganizationSettings settings,
        bool isNew,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken);
}
