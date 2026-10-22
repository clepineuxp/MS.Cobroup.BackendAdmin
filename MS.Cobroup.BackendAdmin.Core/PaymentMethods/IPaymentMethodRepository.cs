using MS.Cobroup.BackendAdmin.Domain.PaymentMethods;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.PaymentMethods;

public interface IPaymentMethodRepository
{
    Task<IReadOnlyCollection<PaymentMethod>> ListAsync(
        Guid organizationId,
        bool activeOnly,
        CancellationToken cancellationToken);

    Task<PaymentMethod?> FindAsync(
        Guid organizationId,
        Guid paymentMethodId,
        CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludedId,
        CancellationToken cancellationToken);

    Task<PaymentMethodSaveOutcome> TryAddAsync(
        PaymentMethod paymentMethod,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken);

    Task<PaymentMethodSaveOutcome> TrySaveAsync(
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken);
}

public enum PaymentMethodSaveOutcome
{
    Saved,
    DuplicateName,
    ConcurrencyConflict
}
