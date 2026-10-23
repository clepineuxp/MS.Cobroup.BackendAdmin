using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

public interface ITokenService
{
    Task<TokenIssueResult> IssueTokensAsync(
        ApplicationUser user,
        OrganizationMembership membership,
        Guid? familyId,
        CancellationToken cancellationToken);

    Task<TokenRefreshResult> RefreshTokensAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken);

    Task<RevokeFamilyResult> RevokeFamilyByTokenAsync(
        string rawRefreshToken,
        string reason,
        CancellationToken cancellationToken);

    Task<OrganizationSwitchResult> SwitchOrganizationSessionAsync(
        Guid expectedUserId,
        Guid expectedCurrentOrgId,
        string rawRefreshToken,
        Guid targetOrgId,
        CancellationToken cancellationToken);
}

public sealed record TokenIssueResult(
    string AccessToken,
    string RawRefreshToken,
    int ExpiresIn,
    Guid FamilyId,
    string TokenType = "Bearer");

public sealed record TokenRefreshResult(
    bool Success,
    string? AccessToken = null,
    string? RawRefreshToken = null,
    int ExpiresIn = 0,
    string? Error = null);

public sealed record RevokeFamilyResult(
    bool Success,
    Guid? FamilyId = null,
    string? Error = null);

public sealed record OrganizationSwitchResult(
    bool Success,
    string? AccessToken = null,
    string? RawRefreshToken = null,
    int ExpiresIn = 0,
    OrganizationMembership? TargetMembership = null,
    int StatusCode = 200,
    string? Error = null);
