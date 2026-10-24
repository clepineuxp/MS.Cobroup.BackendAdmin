using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfOrganizationProvisioningRepository(IdentityDbContext dbContext) : IOrganizationProvisioningRepository
{
    public async Task<bool> TryCreateAsync(Organization organization, ApplicationUser owner,
        OrganizationMembership membership, AdministrativeAuditRecord audit, CancellationToken cancellationToken)
    {
        dbContext.Organizations.Add(organization);
        dbContext.Users.Add(owner);
        dbContext.OrganizationMemberships.Add(membership);
        dbContext.AdministrativeAuditRecords.Add(audit);
        try
        {
            // A single SaveChanges is transactional: no organization can be left without its owner.
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_organizations_Code" or "IX_users_NormalizedUsername" })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
