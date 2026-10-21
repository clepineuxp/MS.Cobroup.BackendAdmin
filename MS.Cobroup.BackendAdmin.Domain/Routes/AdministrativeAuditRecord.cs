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
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        EventType = eventType;
        EntityType = entityType;
        EntityId = entityId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static AdministrativeAuditRecord RouteCreated(
        Guid organizationId,
        Guid actorUserId,
        Guid routeId,
        DateTimeOffset occurredAtUtc) =>
        new(Guid.NewGuid(), organizationId, actorUserId, "route.created", "route", routeId, occurredAtUtc);
}
