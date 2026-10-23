using System.Text.Json;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.PaymentMethods;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.PaymentMethods;

public sealed class PaymentMethodService(IPaymentMethodRepository repository)
{
    public async Task<IReadOnlyCollection<PaymentMethodItem>> ListAvailableAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var items = await repository.ListAsync(organizationId, activeOnly: true, cancellationToken);
        return items.Select(PaymentMethodItem.FromDomain).ToArray();
    }


    public async Task<IReadOnlyCollection<PaymentMethodItem>> ListAllForAdminAsync(
        TenantActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Solo el administrador puede listar medios de pago de auditoría.");
        }

        var items = await repository.ListAsync(actor.OrganizationId, activeOnly: false, cancellationToken);
        return items.Select(PaymentMethodItem.FromDomain).ToArray();
    }

    public async Task<PaymentMethodResult> GetAsync(
        TenantActor actor,
        Guid paymentMethodId,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return PaymentMethodResult.Failure("Solo el administrador puede consultar el detalle del medio de pago.", PaymentMethodErrorType.Forbidden);
        }

        var item = await repository.FindAsync(actor.OrganizationId, paymentMethodId, cancellationToken);
        return item is null
            ? PaymentMethodResult.Failure("Medio de pago no encontrado.", PaymentMethodErrorType.NotFound)
            : PaymentMethodResult.Ok(PaymentMethodItem.FromDomain(item));
    }

    public async Task<PaymentMethodResult> CreateAsync(
        TenantActor actor,
        CreatePaymentMethodInput input,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return PaymentMethodResult.Failure("Solo el administrador puede crear medios de pago.", PaymentMethodErrorType.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return PaymentMethodResult.Failure("El nombre del medio de pago es obligatorio.", PaymentMethodErrorType.ValidationError);
        }

        var trimmedName = input.Name.Trim();
        if (trimmedName.Length > 50)
        {
            return PaymentMethodResult.Failure("El nombre no puede superar 50 caracteres.", PaymentMethodErrorType.ValidationError);
        }

        var normalized = trimmedName.ToUpperInvariant();
        if (await repository.NameExistsAsync(actor.OrganizationId, normalized, null, cancellationToken))
        {
            return PaymentMethodResult.Failure("Ya existe un medio de pago con este nombre en la organización.", PaymentMethodErrorType.DuplicateName);
        }

        var nowUtc = DateTime.UtcNow;
        var paymentMethod = PaymentMethod.Create(actor.OrganizationId, trimmedName, input.DisplayOrder, nowUtc);

        var audit = AdministrativeAuditRecord.PaymentMethodCreated(
            actor.OrganizationId,
            actor.UserId,
            paymentMethod.Id,
            nowUtc,
            JsonSerializer.Serialize(new { paymentMethod.Name, paymentMethod.DisplayOrder, paymentMethod.IsActive }));

        var outcome = await repository.TryAddAsync(paymentMethod, audit, cancellationToken);
        return outcome switch
        {
            PaymentMethodSaveOutcome.Saved => PaymentMethodResult.Ok(PaymentMethodItem.FromDomain(paymentMethod)),
            PaymentMethodSaveOutcome.DuplicateName => PaymentMethodResult.Failure("Ya existe un medio de pago con este nombre.", PaymentMethodErrorType.DuplicateName),
            _ => PaymentMethodResult.Failure("Conflicto de concurrencia al guardar el medio de pago.", PaymentMethodErrorType.Conflict)
        };
    }

    public async Task<PaymentMethodResult> UpdateAsync(
        TenantActor actor,
        Guid paymentMethodId,
        UpdatePaymentMethodInput input,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return PaymentMethodResult.Failure("Solo el administrador puede editar medios de pago.", PaymentMethodErrorType.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return PaymentMethodResult.Failure("El nombre del medio de pago es obligatorio.", PaymentMethodErrorType.ValidationError);
        }

        var trimmedName = input.Name.Trim();
        if (trimmedName.Length > 50)
        {
            return PaymentMethodResult.Failure("El nombre no puede superar 50 caracteres.", PaymentMethodErrorType.ValidationError);
        }

        var paymentMethod = await repository.FindAsync(actor.OrganizationId, paymentMethodId, cancellationToken);
        if (paymentMethod is null)
        {
            return PaymentMethodResult.Failure("Medio de pago no encontrado.", PaymentMethodErrorType.NotFound);
        }

        var normalized = trimmedName.ToUpperInvariant();
        if (await repository.NameExistsAsync(actor.OrganizationId, normalized, paymentMethodId, cancellationToken))
        {
            return PaymentMethodResult.Failure("Ya existe un medio de pago con este nombre en la organización.", PaymentMethodErrorType.DuplicateName);
        }

        var nowUtc = DateTime.UtcNow;
        paymentMethod.Update(trimmedName, input.DisplayOrder, nowUtc);

        var audit = AdministrativeAuditRecord.PaymentMethodUpdated(
            actor.OrganizationId,
            actor.UserId,
            paymentMethod.Id,
            nowUtc,
            JsonSerializer.Serialize(new { paymentMethod.Name, paymentMethod.DisplayOrder, paymentMethod.IsActive }));

        var outcome = await repository.TrySaveAsync(audit, cancellationToken);
        return outcome switch
        {
            PaymentMethodSaveOutcome.Saved => PaymentMethodResult.Ok(PaymentMethodItem.FromDomain(paymentMethod)),
            PaymentMethodSaveOutcome.DuplicateName => PaymentMethodResult.Failure("Ya existe un medio de pago con este nombre.", PaymentMethodErrorType.DuplicateName),
            _ => PaymentMethodResult.Failure("Conflicto de concurrencia al actualizar el medio de pago.", PaymentMethodErrorType.Conflict)
        };
    }

    public async Task<PaymentMethodResult> SetStatusAsync(
        TenantActor actor,
        Guid paymentMethodId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return PaymentMethodResult.Failure("Solo el administrador puede cambiar el estado de medios de pago.", PaymentMethodErrorType.Forbidden);
        }

        var paymentMethod = await repository.FindAsync(actor.OrganizationId, paymentMethodId, cancellationToken);
        if (paymentMethod is null)
        {
            return PaymentMethodResult.Failure("Medio de pago no encontrado.", PaymentMethodErrorType.NotFound);
        }

        var nowUtc = DateTime.UtcNow;
        paymentMethod.SetStatus(isActive, nowUtc);

        var audit = AdministrativeAuditRecord.PaymentMethodStatusChanged(
            actor.OrganizationId,
            actor.UserId,
            paymentMethod.Id,
            nowUtc,
            JsonSerializer.Serialize(new { paymentMethod.Name, paymentMethod.IsActive }));

        var outcome = await repository.TrySaveAsync(audit, cancellationToken);
        return outcome switch
        {
            PaymentMethodSaveOutcome.Saved => PaymentMethodResult.Ok(PaymentMethodItem.FromDomain(paymentMethod)),
            _ => PaymentMethodResult.Failure("Conflicto de concurrencia al cambiar el estado del medio de pago.", PaymentMethodErrorType.Conflict)
        };
    }
}
