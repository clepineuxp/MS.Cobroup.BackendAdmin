using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

public interface ILoanExceptionAuditRepository
{
    Task AddAuditAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken);
}
