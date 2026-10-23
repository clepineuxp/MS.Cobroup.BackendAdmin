using System.Text.RegularExpressions;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public sealed record RevocationExecutionResult(
    string Action,
    string SigningKeyId,
    int AffectedCount,
    DateTimeOffset TimestampUtc,
    string CorrelationId);

public sealed partial class OperationalSessionRevocationService(
    IIdentityRepository identityRepository) : IOperationalSessionRevocationService
{
    public async Task<RevocationExecutionResult> RevokeBySigningKeyIdAsync(
        string signingKeyId,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var correlation = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("D") : correlationId;
        var trimmedKid = signingKeyId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedKid) || !KeyIdRegex().IsMatch(trimmedKid))
        {
            throw new ArgumentException(
                $"El identificador 'kid' ('{trimmedKid}') no cumple con el formato requerido '^[A-Za-z0-9._-]{{1,64}}$'.",
                nameof(signingKeyId));
        }

        var now = DateTimeOffset.UtcNow;
        var count = await identityRepository.RevokeRefreshTokensBySigningKeyIdAsync(
            trimmedKid,
            now,
            $"Operational retirement of signing key '{trimmedKid}'",
            cancellationToken);

        return new RevocationExecutionResult(
            Action: "revoke-kid",
            SigningKeyId: trimmedKid,
            AffectedCount: count,
            TimestampUtc: now,
            CorrelationId: correlation);
    }

    public async Task<RevocationExecutionResult> RevokeAllAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var correlation = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("D") : correlationId;
        var now = DateTimeOffset.UtcNow;

        var count = await identityRepository.RevokeAllRefreshTokensAsync(
            now,
            "Emergency key compromise - All active sessions revoked",
            cancellationToken);

        return new RevocationExecutionResult(
            Action: "revoke-all",
            SigningKeyId: "*",
            AffectedCount: count,
            TimestampUtc: now,
            CorrelationId: correlation);
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex KeyIdRegex();
}
