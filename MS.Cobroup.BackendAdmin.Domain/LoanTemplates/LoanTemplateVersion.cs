namespace MS.Cobroup.BackendAdmin.Domain.LoanTemplates;

public sealed class LoanTemplateVersion
{
    private LoanTemplateVersion()
    {
    }

    internal LoanTemplateVersion(
        Guid loanTemplateId,
        int version,
        string name,
        string? description,
        LoanTemplateClassification classification,
        LoanTemplateStatus status,
        int maxLoanDays,
        decimal maxPrincipalAmount,
        string allowedPercentagesJson,
        Guid recordedByUserId,
        DateTimeOffset recordedAtUtc, decimal minPrincipalAmount, string routeIdsJson)
    {
        Id = Guid.NewGuid();
        LoanTemplateId = loanTemplateId;
        Version = version;
        Name = name;
        Description = description;
        Classification = classification;
        Status = status;
        MaxLoanDays = maxLoanDays;
        MaxPrincipalAmount = maxPrincipalAmount;
        MinPrincipalAmount = minPrincipalAmount;
        RouteIdsJson = routeIdsJson;
        AllowedPercentagesJson = allowedPercentagesJson;
        RecordedByUserId = recordedByUserId;
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid LoanTemplateId { get; private set; }

    public int Version { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public LoanTemplateClassification Classification { get; private set; }

    public LoanTemplateStatus Status { get; private set; }

    public int MaxLoanDays { get; private set; }

    public decimal MinPrincipalAmount { get; private set; }
    public string RouteIdsJson { get; private set; } = "[]";

    public decimal MaxPrincipalAmount { get; private set; }

    public string AllowedPercentagesJson { get; private set; } = "[]";

    public Guid RecordedByUserId { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }
}
