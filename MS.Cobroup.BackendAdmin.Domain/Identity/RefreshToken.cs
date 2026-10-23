using System.Text.RegularExpressions;

namespace MS.Cobroup.BackendAdmin.Domain.Identity;

public sealed partial class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string SigningKeyId { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? RevocationReason { get; private set; }

    public bool IsActive => RevokedAtUtc is null && DateTimeOffset.UtcNow < ExpiresAtUtc;

    private RefreshToken() { }

    public RefreshToken(
        Guid familyId,
        Guid userId,
        Guid organizationId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc,
        string signingKeyId)
    {
        if (string.IsNullOrWhiteSpace(signingKeyId) || !SigningKeyIdRegex().IsMatch(signingKeyId.Trim()))
        {
            throw new ArgumentException(
                $"El identificador de clave de firma '{signingKeyId}' es obligatorio y debe cumplir con el formato '^[A-Za-z0-9._-]{{1,64}}$'.",
                nameof(signingKeyId));
        }

        Id = Guid.NewGuid();
        FamilyId = familyId;
        UserId = userId;
        OrganizationId = organizationId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        SigningKeyId = signingKeyId.Trim();
    }

    public void Revoke(DateTimeOffset revokedAtUtc, string? replacedByTokenHash = null, string? reason = null)
    {
        RevokedAtUtc = revokedAtUtc;
        ReplacedByTokenHash = replacedByTokenHash;
        RevocationReason = reason;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SigningKeyIdRegex();
}
