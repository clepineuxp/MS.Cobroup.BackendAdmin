using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.LoanExceptions;

public interface ILoanExceptionAuditRepository
{
    Task AddAuditAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken);
}
