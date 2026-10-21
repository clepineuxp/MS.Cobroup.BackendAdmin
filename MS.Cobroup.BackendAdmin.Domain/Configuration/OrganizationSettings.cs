namespace MS.Cobroup.BackendAdmin.Domain.Configuration;

public sealed class OrganizationSettings
{
    private OrganizationSettings()
    {
    }

    private OrganizationSettings(
        Guid organizationId,
        string timeZoneId,
        string currencyCode,
        int? maxActiveCustomersPerRoute,
        int? maxActiveLoansPerRoute,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        OrganizationId = RequireId(organizationId, nameof(organizationId));
        Apply(timeZoneId, currencyCode, maxActiveCustomersPerRoute, maxActiveLoansPerRoute, updatedByUserId, updatedAtUtc);
    }

    public Guid OrganizationId { get; private set; }

    public string TimeZoneId { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public int? MaxActiveCustomersPerRoute { get; private set; }

    public int? MaxActiveLoansPerRoute { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static OrganizationSettings Create(
        Guid organizationId,
        string timeZoneId,
        string currencyCode,
        int? maxActiveCustomersPerRoute,
        int? maxActiveLoansPerRoute,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc) =>
        new(
            organizationId,
            timeZoneId,
            currencyCode,
            maxActiveCustomersPerRoute,
            maxActiveLoansPerRoute,
            updatedByUserId,
            updatedAtUtc);

    public void Update(
        string timeZoneId,
        string currencyCode,
        int? maxActiveCustomersPerRoute,
        int? maxActiveLoansPerRoute,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc) =>
        Apply(timeZoneId, currencyCode, maxActiveCustomersPerRoute, maxActiveLoansPerRoute, updatedByUserId, updatedAtUtc);

    private void Apply(
        string timeZoneId,
        string currencyCode,
        int? maxActiveCustomersPerRoute,
        int? maxActiveLoansPerRoute,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        TimeZoneId = Require(timeZoneId, nameof(timeZoneId), 100);
        CurrencyCode = Require(currencyCode, nameof(currencyCode), 3).ToUpperInvariant();
        if (CurrencyCode.Length != 3 || CurrencyCode.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("currencyCode must be a three-letter ISO code.", nameof(currencyCode));
        }

        MaxActiveCustomersPerRoute = RequirePositive(maxActiveCustomersPerRoute, nameof(maxActiveCustomersPerRoute));
        MaxActiveLoansPerRoute = RequirePositive(maxActiveLoansPerRoute, nameof(maxActiveLoansPerRoute));
        UpdatedByUserId = RequireId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = updatedAtUtc;
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("An identifier is required.", parameterName) : value;

    private static int? RequirePositive(int? value, string parameterName) =>
        value is <= 0 ? throw new ArgumentOutOfRangeException(parameterName, "A configured limit must be greater than zero.") : value;

    private static string Require(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} must contain between 1 and {maximumLength} characters.", parameterName);
        }

        return normalized;
    }
}
