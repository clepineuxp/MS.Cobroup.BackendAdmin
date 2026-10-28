namespace MS.Cobroup.BackendAdmin.Domain.MachineAuth;

public sealed class MachineAuthorizationRequest
{
    private MachineAuthorizationRequest() { }
    public MachineAuthorizationRequest(Guid organizationId, Guid routeId, Guid userId, string machineId,
        string otpHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc, string? protectedOtp = null)
    { Id = Guid.NewGuid(); OrganizationId = organizationId; RouteId = routeId; UserId = userId; MachineId = machineId; OtpHash = otpHash; ProtectedOtp = protectedOtp; CreatedAtUtc = createdAtUtc; ExpiresAtUtc = expiresAtUtc; }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid UserId { get; private set; }
    public string MachineId { get; private set; } = string.Empty;
    public string OtpHash { get; private set; } = string.Empty;
    public string? ProtectedOtp { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public bool IsConsumed => ApprovedAtUtc.HasValue;
    public bool IsCancelled => CancelledAtUtc.HasValue;
    public bool IsValid(DateTimeOffset now) => !IsConsumed && !IsCancelled && ExpiresAtUtc > now;
    public void Approve(Guid actorUserId, DateTimeOffset now) { ApprovedAtUtc = now; ApprovedByUserId = actorUserId; }
    public void Cancel(Guid actorUserId, DateTimeOffset now) { CancelledAtUtc = now; CancelledByUserId = actorUserId; }
}
