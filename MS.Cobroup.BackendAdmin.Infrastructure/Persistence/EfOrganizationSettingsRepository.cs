using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Core.Configuration;
using MS.Cobroup.BackendAdmin.Domain.Configuration;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfOrganizationSettingsRepository(IdentityDbContext dbContext)
    : IOrganizationSettingsRepository
{
    public Task<OrganizationSettings?> FindAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.OrganizationSettings.SingleOrDefaultAsync(
            item => item.OrganizationId == organizationId,
            cancellationToken);

    public async Task SaveAsync(
        OrganizationSettings settings,
        bool isNew,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (isNew)
        {
            dbContext.OrganizationSettings.Add(settings);
        }

        dbContext.AdministrativeAuditRecords.Add(auditRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
