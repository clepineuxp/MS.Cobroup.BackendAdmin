namespace MS.Cobroup.BackendAdmin.Domain.Routes;

public sealed class CollectionRoute
{
    private readonly List<RouteCollectorAssignment> collectorAssignments = [];

    private CollectionRoute()
    {
    }

    private CollectionRoute(
        Guid id,
        Guid organizationId,
        string name,
        string? description,
        Guid supervisorUserId,
        IReadOnlyCollection<Guid> collectorUserIds,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc,
        RouteSettingMode maxActiveCustomersMode,
        int? maxActiveCustomersOverride,
        RouteSettingMode maxActiveLoansMode,
        int? maxActiveLoansOverride)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An organization is required.", nameof(organizationId));
        }

        if (supervisorUserId == Guid.Empty)
        {
            throw new ArgumentException("A supervisor is required.", nameof(supervisorUserId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("A creator is required.", nameof(createdByUserId));
        }

        Id = id;
        OrganizationId = organizationId;
        Name = Require(name, nameof(name), 120);
        NormalizedName = NormalizeName(name);
        Description = NormalizeOptional(description, nameof(description), 500);
        Status = CollectionRouteStatus.Active;
        MaxActiveCustomersMode = maxActiveCustomersMode;
        MaxActiveCustomersOverride = ValidateOverride(maxActiveCustomersMode, maxActiveCustomersOverride, nameof(maxActiveCustomersOverride));
        MaxActiveLoansMode = maxActiveLoansMode;
        MaxActiveLoansOverride = ValidateOverride(maxActiveLoansMode, maxActiveLoansOverride, nameof(maxActiveLoansOverride));
        SupervisorUserId = supervisorUserId;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;

        foreach (var collectorUserId in collectorUserIds.Distinct())
        {
            if (collectorUserId == Guid.Empty)
            {
                throw new ArgumentException("Collector identifiers cannot be empty.", nameof(collectorUserIds));
            }

            collectorAssignments.Add(new RouteCollectorAssignment(Id, collectorUserId, createdByUserId, createdAtUtc));
        }
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public CollectionRouteStatus Status { get; private set; }

    public Guid SupervisorUserId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public RouteSettingMode MaxActiveCustomersMode { get; private set; }

    public int? MaxActiveCustomersOverride { get; private set; }

    public RouteSettingMode MaxActiveLoansMode { get; private set; }

    public int? MaxActiveLoansOverride { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<RouteCollectorAssignment> CollectorAssignments => collectorAssignments.AsReadOnly();

    public static CollectionRoute Create(
        Guid organizationId,
        string name,
        string? description,
        Guid supervisorUserId,
        IReadOnlyCollection<Guid> collectorUserIds,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc,
        RouteSettingMode maxActiveCustomersMode = RouteSettingMode.Inherited,
        int? maxActiveCustomersOverride = null,
        RouteSettingMode maxActiveLoansMode = RouteSettingMode.Inherited,
        int? maxActiveLoansOverride = null) =>
        new(
            Guid.NewGuid(),
            organizationId,
            name,
            description,
            supervisorUserId,
            collectorUserIds,
            createdByUserId,
            createdAtUtc,
            maxActiveCustomersMode,
            maxActiveCustomersOverride,
            maxActiveLoansMode,
            maxActiveLoansOverride);

    public static string NormalizeName(string value) =>
        Require(value, nameof(value), 120).ToUpperInvariant();

    public void Update(
        string name,
        string? description,
        CollectionRouteStatus status,
        Guid supervisorUserId,
        IReadOnlyCollection<Guid> collectorUserIds,
        RouteSettingMode maxActiveCustomersMode,
        int? maxActiveCustomersOverride,
        RouteSettingMode maxActiveLoansMode,
        int? maxActiveLoansOverride,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        if (supervisorUserId == Guid.Empty || updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Supervisor and updater identifiers are required.");
        }

        Name = Require(name, nameof(name), 120);
        NormalizedName = NormalizeName(name);
        Description = NormalizeOptional(description, nameof(description), 500);
        Status = status;
        SupervisorUserId = supervisorUserId;
        MaxActiveCustomersMode = maxActiveCustomersMode;
        MaxActiveCustomersOverride = ValidateOverride(maxActiveCustomersMode, maxActiveCustomersOverride, nameof(maxActiveCustomersOverride));
        MaxActiveLoansMode = maxActiveLoansMode;
        MaxActiveLoansOverride = ValidateOverride(maxActiveLoansMode, maxActiveLoansOverride, nameof(maxActiveLoansOverride));
        UpdatedAtUtc = updatedAtUtc;

        var requestedCollectorIds = collectorUserIds.Distinct().ToHashSet();
        if (requestedCollectorIds.Contains(Guid.Empty))
        {
            throw new ArgumentException("Collector identifiers cannot be empty.", nameof(collectorUserIds));
        }

        collectorAssignments.RemoveAll(item => !requestedCollectorIds.Contains(item.CollectorUserId));
        var currentCollectorIds = collectorAssignments.Select(item => item.CollectorUserId).ToHashSet();
        foreach (var collectorUserId in requestedCollectorIds.Except(currentCollectorIds))
        {
            collectorAssignments.Add(new RouteCollectorAssignment(Id, collectorUserId, updatedByUserId, updatedAtUtc));
        }
    }

    public int? ResolveMaxActiveCustomers(int? organizationValue) =>
        MaxActiveCustomersMode is RouteSettingMode.Inherited ? organizationValue : MaxActiveCustomersOverride;

    public int? ResolveMaxActiveLoans(int? organizationValue) =>
        MaxActiveLoansMode is RouteSettingMode.Inherited ? organizationValue : MaxActiveLoansOverride;

    private static int? ValidateOverride(RouteSettingMode mode, int? value, string parameterName)
    {
        if (mode is RouteSettingMode.Inherited)
        {
            return null;
        }

        return value is > 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, "An overridden limit must be greater than zero.");
    }

    private static string Require(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} must contain between 1 and {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }
}
