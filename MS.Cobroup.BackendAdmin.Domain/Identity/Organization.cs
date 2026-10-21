namespace MS.Cobroup.BackendAdmin.Domain.Identity;

public sealed class Organization
{
    private Organization()
    {
    }

    private Organization(Guid id, string code, string name, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Code = NormalizeCode(code);
        Name = Require(name, nameof(name), 160);
        Status = OrganizationStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public OrganizationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Organization Create(string code, string name, DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), code, name, createdAtUtc);

    public static string NormalizeCode(string value) =>
        Require(value, nameof(value), 48).ToUpperInvariant();

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
