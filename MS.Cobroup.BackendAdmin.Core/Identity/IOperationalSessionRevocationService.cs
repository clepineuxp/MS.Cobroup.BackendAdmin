namespace MS.Cobroup.BackendAdmin.Core.Identity;

public interface IOperationalSessionRevocationService
{
    Task<RevocationExecutionResult> RevokeBySigningKeyIdAsync(
        string signingKeyId,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    Task<RevocationExecutionResult> RevokeAllAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
