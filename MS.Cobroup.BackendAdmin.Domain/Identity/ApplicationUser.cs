using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Domain.Identity;

public sealed class ApplicationUser
{
    private ApplicationUser()
    {
    }

    private ApplicationUser(
        Guid id,
        Guid organizationId,
        string username,
        string displayName,
        string passwordHash,
        UserRole role,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        Username = Require(username, nameof(username), 80);
        NormalizedUsername = NormalizeUsername(username);
        DisplayName = Require(displayName, nameof(displayName), 160);
        PasswordHash = Require(passwordHash, nameof(passwordHash), 512);
        Role = role;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ApplicationUser CreateAdministrator(
        Guid organizationId,
        string username,
        string displayName,
        string passwordHash,
        DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), organizationId, username, displayName, passwordHash, UserRole.Admin, createdAtUtc);

    public static ApplicationUser Create(
        Guid organizationId,
        string username,
        string displayName,
        string passwordHash,
        UserRole role,
        DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), organizationId, username, displayName, passwordHash, role, createdAtUtc);

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
