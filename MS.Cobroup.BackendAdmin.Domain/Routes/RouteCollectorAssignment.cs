namespace MS.Cobroup.BackendAdmin.Domain.Routes;

public sealed class RouteCollectorAssignment
{
    private RouteCollectorAssignment()
    {
    }

    internal RouteCollectorAssignment(
        Guid routeId,
        Guid collectorUserId,
        Guid assignedByUserId,
        DateTimeOffset assignedAtUtc)
    {
        RouteId = routeId;
        CollectorUserId = collectorUserId;
        AssignedByUserId = assignedByUserId;
        AssignedAtUtc = assignedAtUtc;
    }

    public Guid RouteId { get; private set; }

    public CollectionRoute Route { get; private set; } = null!;

    public Guid CollectorUserId { get; private set; }

    public Guid AssignedByUserId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }
}
