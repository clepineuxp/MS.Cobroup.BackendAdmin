using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

public interface IPasswordCredentialService
{
    string Hash(string password);

    bool Verify(ApplicationUser user, string password);
}
