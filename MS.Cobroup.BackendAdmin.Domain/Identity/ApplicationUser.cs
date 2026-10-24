namespace MS.Cobroup.BackendAdmin.Domain.Identity;

public sealed class ApplicationUser
{
    private ApplicationUser()
    {
    }

    private ApplicationUser(
        Guid id,
        string username,
        string displayName,
        string passwordHash,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Username = Require(username, nameof(username), 80);
        NormalizedUsername = NormalizeUsername(username);
        DisplayName = Require(displayName, nameof(displayName), 160);
        PasswordHash = Require(passwordHash, nameof(passwordHash), 512);
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string? PhoneNumber { get; private set; }

    public static ApplicationUser Create(
        string username,
        string displayName,
        string passwordHash,
        DateTimeOffset createdAtUtc,
        string? phoneNumber = null)
    {
        var user = new ApplicationUser(Guid.NewGuid(), username, displayName, passwordHash, createdAtUtc);
        user.UpdatedAtUtc = createdAtUtc;
        user.PhoneNumber = phoneNumber is null ? null : Require(phoneNumber, nameof(phoneNumber), 24);
        return user;
    }

    public static string NormalizeUsername(string value) =>
        Require(value, nameof(value), 80).ToUpperInvariant();

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
