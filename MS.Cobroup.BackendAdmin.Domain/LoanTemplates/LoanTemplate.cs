using System.Text.Json;

namespace MS.Cobroup.BackendAdmin.Domain.LoanTemplates;

public sealed class LoanTemplate
{
    private readonly List<LoanTemplateVersion> versions = [];

    private LoanTemplate()
    {
    }

    private LoanTemplate(
        Guid organizationId,
        string name,
        string? description,
        LoanTemplateClassification classification,
        LoanTemplateStatus status,
        int maxLoanDays,
        decimal maxPrincipalAmount,
        IReadOnlyCollection<decimal> allowedPercentages,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (organizationId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Organization and creator are required.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = Require(name, nameof(name), 120);
        NormalizedName = NormalizeName(name);
        Description = NormalizeOptional(description, nameof(description), 500);
        Classification = ValidateClassification(classification);
        Status = ValidateStatus(status);
        MaxLoanDays = ValidateDays(maxLoanDays);
        MaxPrincipalAmount = ValidateAmount(maxPrincipalAmount);
        AllowedPercentagesJson = SerializePercentages(allowedPercentages);
        Version = 1;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        AddVersion(createdByUserId, createdAtUtc);
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public LoanTemplateClassification Classification { get; private set; }

    public LoanTemplateStatus Status { get; private set; }

    public int Version { get; private set; }

    public int MaxLoanDays { get; private set; }

    public decimal MaxPrincipalAmount { get; private set; }

    public string AllowedPercentagesJson { get; private set; } = "[]";

    public Guid CreatedByUserId { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<decimal> AllowedPercentages =>
        JsonSerializer.Deserialize<decimal[]>(AllowedPercentagesJson) ?? [];

    public IReadOnlyCollection<LoanTemplateVersion> Versions => versions.AsReadOnly();

    public static LoanTemplate Create(
        Guid organizationId,
        string name,
        string? description,
        LoanTemplateClassification classification,
        LoanTemplateStatus status,
        int maxLoanDays,
        decimal maxPrincipalAmount,
        IReadOnlyCollection<decimal> allowedPercentages,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc) =>
        new(
            organizationId,
            name,
            description,
            classification,
            status,
            maxLoanDays,
            maxPrincipalAmount,
            allowedPercentages,
            createdByUserId,
            createdAtUtc);

    public void Update(
        string name,
        string? description,
        LoanTemplateClassification classification,
        LoanTemplateStatus status,
        int maxLoanDays,
        decimal maxPrincipalAmount,
        IReadOnlyCollection<decimal> allowedPercentages,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        if (updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Updater is required.", nameof(updatedByUserId));
        }

        Name = Require(name, nameof(name), 120);
        NormalizedName = NormalizeName(name);
        Description = NormalizeOptional(description, nameof(description), 500);
        Classification = ValidateClassification(classification);
        Status = ValidateStatus(status);
        MaxLoanDays = ValidateDays(maxLoanDays);
        MaxPrincipalAmount = ValidateAmount(maxPrincipalAmount);
        AllowedPercentagesJson = SerializePercentages(allowedPercentages);
        Version++;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
        AddVersion(updatedByUserId, updatedAtUtc);
    }

    public static string NormalizeName(string value) =>
        Require(value, nameof(value), 120).ToUpperInvariant();

    private void AddVersion(Guid actorUserId, DateTimeOffset recordedAtUtc) =>
        versions.Add(new LoanTemplateVersion(
            Id,
            Version,
            Name,
            Description,
            Classification,
            Status,
            MaxLoanDays,
            MaxPrincipalAmount,
            AllowedPercentagesJson,
            actorUserId,
            recordedAtUtc));

    private static int ValidateDays(int value) => value is > 0 and <= 3650
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), "Maximum loan days must be between 1 and 3650.");

    private static decimal ValidateAmount(decimal value) => value is > 0 and < 100000000000000m && GetScale(value) <= 4
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), "Maximum principal amount must be positive with at most 4 decimal places.");

    private static string SerializePercentages(IReadOnlyCollection<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var normalized = values.Distinct().Order().ToArray();
        if (normalized.Length is < 1 or > 20
            || normalized.Any(value => value <= 0 || value >= 100000m || GetScale(value) > 4))
        {
            throw new ArgumentException("Between 1 and 20 positive percentages are required.", nameof(values));
        }

        return JsonSerializer.Serialize(normalized);
    }

    private static LoanTemplateClassification ValidateClassification(LoanTemplateClassification value) =>
        Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Loan template classification is invalid.");

    private static LoanTemplateStatus ValidateStatus(LoanTemplateStatus value) =>
        Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Loan template status is invalid.");

    private static byte GetScale(decimal value) => (byte)((decimal.GetBits(value)[3] >> 16) & 0x7F);

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
