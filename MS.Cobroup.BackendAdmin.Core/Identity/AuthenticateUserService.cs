using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public sealed class AuthenticateUserService(IIdentityRepository repository, IPasswordCredentialService passwords)
{
    public async Task<ApplicationUser?> ExecuteAsync(
        string organizationCode,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await repository.FindActiveUserAsync(
            Organization.NormalizeCode(organizationCode),
            ApplicationUser.NormalizeUsername(username),
            cancellationToken);

        return user is not null && passwords.Verify(user, password) ? user : null;
    }
}
