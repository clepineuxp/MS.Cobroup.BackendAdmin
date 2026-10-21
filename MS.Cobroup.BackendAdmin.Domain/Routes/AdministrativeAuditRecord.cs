namespace MS.Cobroup.BackendAdmin.Domain.Routes;

public sealed class AdministrativeAuditRecord
{
    private AdministrativeAuditRecord()
    {
    }

    private AdministrativeAuditRecord(
        Guid id,
        Guid organizationId,
        Guid actorUserId,
        string eventType,
        string entityType,
        Guid entityId,
        DateTimeOffset occurredAtUtc,
        string? detailsJson)
    {
        Id = id;
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        EventType = eventType;
        EntityType = entityType;
        EntityId = entityId;
        OccurredAtUtc = occurredAtUtc;
        DetailsJson = detailsJson;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? DetailsJson { get; private set; }

    public static AdministrativeAuditRecord RouteCreated(
        Guid organizationId,
        Guid actorUserId,
        Guid routeId,
        DateTimeOffset occurredAtUtc) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "route.created", "route", routeId, occurredAtUtc, null);

    public static AdministrativeAuditRecord RouteUpdated(
        Guid organizationId,
        Guid actorUserId,
        Guid routeId,
        DateTimeOffset occurredAtUtc,
        string detailsJson) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "route.updated", "route", routeId, occurredAtUtc, detailsJson);

    public static AdministrativeAuditRecord OrganizationSettingsUpdated(
        Guid organizationId,
        Guid actorUserId,
        DateTimeOffset occurredAtUtc,
        string detailsJson) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "organization.settings.updated", "organization_settings", organizationId, occurredAtUtc, detailsJson);

    public static AdministrativeAuditRecord LoanTemplateCreated(
        Guid organizationId,
        Guid actorUserId,
        Guid templateId,
        DateTimeOffset occurredAtUtc,
        string detailsJson) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "loan_template.created", "loan_template", templateId, occurredAtUtc, detailsJson);

    public static AdministrativeAuditRecord LoanTemplateUpdated(
        Guid organizationId,
        Guid actorUserId,
        Guid templateId,
        DateTimeOffset occurredAtUtc,
        string detailsJson) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "loan_template.updated", "loan_template", templateId, occurredAtUtc, detailsJson);

    public static AdministrativeAuditRecord LoanExceptionDecided(
        Guid organizationId,
        Guid actorUserId,
        Guid exceptionId,
        string decision,
        DateTimeOffset occurredAtUtc,
        string detailsJson) =>
        new(Guid.NewGuid(), organizationId, actorUserId, $"loan_exception.{decision.ToLowerInvariant()}", "loan_exception", exceptionId, occurredAtUtc, detailsJson);

    public static AdministrativeAuditRecord PaymentMethodCreated(
        Guid organizationId,
        Guid actorUserId,
        Guid paymentMethodId,
        DateTimeOffset occurredAtUtc,
        string detailsJson) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "payment_method.created", "payment_method", paymentMethodId, occurredAtUtc, detailsJson);

    public static AdministrativeAuditRecord PaymentMethodUpdated(
        Guid organizationId,
        Guid actorUserId,
        Guid paymentMethodId,
        DateTimeOffset occurredAtUtc,
        string detailsJson) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "payment_method.updated", "payment_method", paymentMethodId, occurredAtUtc, detailsJson);

    public static AdministrativeAuditRecord PaymentMethodStatusChanged(
        Guid organizationId,
        Guid actorUserId,
        Guid paymentMethodId,
        DateTimeOffset occurredAtUtc,
        string detailsJson) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "payment_method.status_changed", "payment_method", paymentMethodId, occurredAtUtc, detailsJson);
}

