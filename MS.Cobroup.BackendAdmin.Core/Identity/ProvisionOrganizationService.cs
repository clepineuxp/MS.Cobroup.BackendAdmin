using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public sealed class ProvisionOrganizationService(IOrganizationProvisioningRepository repository, IPasswordCredentialService passwords)
{
    public async Task<ProvisionOrganizationResult> ExecuteAsync(ProvisionOrganizationCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var organization = Organization.Create(command.OrganizationCode, command.OrganizationName, now);
        UserCredentialValidation.Validate(command.Username, command.DisplayName, command.Password, command.PhoneNumber);
        var owner = ApplicationUser.Create(command.Username, command.DisplayName, passwords.Hash(command.Password), now, command.PhoneNumber);
        var membership = OrganizationMembership.CreateOwner(owner.Id, organization.Id, now);
        var audit = AdministrativeAuditRecord.OrganizationProvisioned(organization.Id, owner.Id, now);
        var created = await repository.TryCreateAsync(organization, owner, membership, audit, cancellationToken);
        return created
            ? new(true, organization.Id, owner.Id)
            : new(false, null, null);
    }
}

public sealed record ProvisionOrganizationCommand(string OrganizationCode, string OrganizationName,
    string Username, string DisplayName, string Password, string PhoneNumber);
public sealed record ProvisionOrganizationResult(bool Created, Guid? OrganizationId, Guid? OwnerUserId);
