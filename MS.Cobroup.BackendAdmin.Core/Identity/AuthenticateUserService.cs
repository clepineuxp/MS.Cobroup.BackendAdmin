using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public sealed class AuthenticateUserService(IIdentityRepository repository, IPasswordCredentialService passwords)
{
    public async Task<AuthenticationResult> ExecuteAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await repository.FindActiveUserAsync(
            ApplicationUser.NormalizeUsername(username),
            cancellationToken);

        if (user is null || !passwords.Verify(user, password))
        {
            return AuthenticationResult.InvalidCredentials();
        }

        var membership = await repository.SelectMostRecentlyAccessedMembershipAsync(
            user.Id,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return membership is null
            ? AuthenticationResult.MembershipRequired(user)
            : AuthenticationResult.Authenticated(user, membership);
    }
}

public enum AuthenticationOutcome
{
    Authenticated,
    InvalidCredentials,
    MembershipRequired
}

public sealed record AuthenticationResult(
    AuthenticationOutcome Outcome,
    ApplicationUser? User,
    OrganizationMembership? Membership)
{
    public static AuthenticationResult Authenticated(
        ApplicationUser user,
        OrganizationMembership membership) =>
        new(AuthenticationOutcome.Authenticated, user, membership);

    public static AuthenticationResult InvalidCredentials() =>
        new(AuthenticationOutcome.InvalidCredentials, null, null);

    public static AuthenticationResult MembershipRequired(ApplicationUser user) =>
        new(AuthenticationOutcome.MembershipRequired, user, null);
}
