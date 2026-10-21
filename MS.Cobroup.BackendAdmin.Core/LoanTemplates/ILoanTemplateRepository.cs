using MS.Cobroup.BackendAdmin.Domain.LoanTemplates;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.LoanTemplates;

public interface ILoanTemplateRepository
{
    Task<IReadOnlyCollection<LoanTemplate>> ListAsync(
        Guid organizationId,
        bool publishedOnly,
        CancellationToken cancellationToken);

    Task<LoanTemplate?> FindAsync(
        Guid organizationId,
        Guid templateId,
        CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludedTemplateId,
        CancellationToken cancellationToken);

    Task<LoanTemplateSaveOutcome> TryAddAsync(
        LoanTemplate template,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken);

    Task<LoanTemplateSaveOutcome> TrySaveAsync(
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken);
}

public enum LoanTemplateSaveOutcome
{
    Saved,
    DuplicateName,
    ConcurrencyConflict
}
