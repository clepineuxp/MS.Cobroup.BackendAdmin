using Microsoft.AspNetCore.Identity;
using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Security;

public sealed class AspNetPasswordCredentialService(IPasswordHasher<object> passwordHasher)
    : IPasswordCredentialService
{
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length < 12)
        {
            throw new ArgumentException("Password must contain at least 12 characters.", nameof(password));
        }

        return passwordHasher.HashPassword(this, password);
    }

    public bool Verify(ApplicationUser user, string password) =>
        passwordHasher.VerifyHashedPassword(this, user.PasswordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
