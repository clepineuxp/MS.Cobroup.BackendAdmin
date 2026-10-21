using MS.Cobroup.BackendAdmin.Domain.PaymentMethods;

namespace MS.Cobroup.BackendAdmin.Core.PaymentMethods;

public sealed record PaymentMethodItem(
    Guid Id,
    string Name,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static PaymentMethodItem FromDomain(PaymentMethod entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.DisplayOrder,
            entity.IsActive,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
}

public sealed record CreatePaymentMethodInput(
    string Name,
    int DisplayOrder);

public sealed record UpdatePaymentMethodInput(
    string Name,
    int DisplayOrder);

public sealed record PaymentMethodResult(
    bool Success,
    string? ErrorMessage,
    PaymentMethodItem? Item,
    PaymentMethodErrorType? ErrorType = null)
{
    public static PaymentMethodResult Ok(PaymentMethodItem item) =>
        new(true, null, item);

    public static PaymentMethodResult Failure(string error, PaymentMethodErrorType errorType) =>
        new(false, error, null, errorType);
}

public enum PaymentMethodErrorType
{
    Forbidden,
    NotFound,
    ValidationError,
    DuplicateName,
    Conflict
}
