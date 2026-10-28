namespace MS.Cobroup.BackendAdmin.Domain.MachineAuth;

public enum MachineAuthorizationStatus { Active, Revoked }

public sealed class MachineAuthorization
{
    private MachineAuthorization() { }

    public MachineAuthorization(Guid organizationId, Guid routeId, Guid userId, string machineId,
        DateTimeOffset createdAtUtc, DateTimeOffset? authorizedUntilUtc, bool indefinite)
    {
        Id = Guid.NewGuid(); OrganizationId = organizationId; RouteId = routeId; UserId = userId;
        MachineId = RequireMachineId(machineId); CreatedAtUtc = createdAtUtc;
        AuthorizedUntilUtc = indefinite ? null : authorizedUntilUtc;
        IsIndefinite = indefinite; Status = MachineAuthorizationStatus.Active;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid UserId { get; private set; }
    public string MachineId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AuthorizedUntilUtc { get; private set; }
    public bool IsIndefinite { get; private set; }
    public MachineAuthorizationStatus Status { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    public bool IsCurrentlyActive(DateTimeOffset now) => Status == MachineAuthorizationStatus.Active &&
        (IsIndefinite || (AuthorizedUntilUtc.HasValue && AuthorizedUntilUtc.Value > now));

    public void Revoke(Guid actorUserId, DateTimeOffset now)
    { Status = MachineAuthorizationStatus.Revoked; RevokedAtUtc = now; RevokedByUserId = actorUserId; }

    private static string RequireMachineId(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 16 or > 128) throw new ArgumentException("El identificador de máquina no es válido.", nameof(value));
        return normalized;
    }
}
