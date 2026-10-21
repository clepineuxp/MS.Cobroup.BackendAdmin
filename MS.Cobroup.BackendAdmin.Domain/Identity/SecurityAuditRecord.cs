namespace MS.Cobroup.BackendAdmin.Domain.Identity;

public sealed class SecurityAuditRecord
{
    private SecurityAuditRecord()
    {
    }

    private SecurityAuditRecord(Guid id, string eventType, string actor, string sourceIp, DateTimeOffset occurredAtUtc)
    {
        Id = id;
        EventType = eventType;
        Actor = actor;
        SourceIp = sourceIp;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Actor { get; private set; } = string.Empty;

    public string SourceIp { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static SecurityAuditRecord BootstrapCompleted(string actor, string sourceIp, DateTimeOffset occurredAtUtc) =>
        new(Guid.NewGuid(), "identity.bootstrap.completed", actor, sourceIp, occurredAtUtc);
}
