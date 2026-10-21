using MS.Cobroup.BackendAdmin.Core.LoanExceptions;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfLoanExceptionAuditRepository(IdentityDbContext dbContext) : ILoanExceptionAuditRepository
{
    public async Task AddAuditAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken)
    {
        await dbContext.AdministrativeAuditRecords.AddAsync(auditRecord, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
