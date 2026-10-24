using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Identity;

public sealed class ProvisionOrganizationServiceTests
{
    [Fact]
    public async Task ProvisionsOrganizationAndExactlyOneOwnerInSingleRepositoryOperation()
    {
        var repository = new MemoryProvisioning();
        var result = await new ProvisionOrganizationService(repository, new FakePasswords()).ExecuteAsync(
            new(" norte ", " Organización Norte ", "owner.norte", "Dueño Norte", "1", "+573001234567"), default);
        Assert.True(result.Created);
        Assert.Equal("NORTE", repository.Organization!.Code);
        Assert.Equal(result.OrganizationId, repository.Membership!.OrganizationId);
        Assert.Equal(result.OwnerUserId, repository.Membership.UserId);
        Assert.Equal(UserRole.Admin, repository.Membership.Role);
        Assert.True(repository.Membership.IsOwner);
        Assert.True(repository.Membership.IsActive);
        Assert.Equal("hash", repository.Owner!.PasswordHash);
        Assert.Equal("organization.provisioned", repository.Audit!.EventType);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task DuplicateOrganizationOrUsernameDoesNotReportCreatedIds()
    {
        var repository = new MemoryProvisioning { Created = false };
        var result = await new ProvisionOrganizationService(repository, new FakePasswords()).ExecuteAsync(
            new("norte", "Norte", "owner", "Owner", "1", "+573001234567"), default);
        Assert.False(result.Created);
        Assert.Null(result.OrganizationId);
        Assert.Null(result.OwnerUserId);
    }

    [Fact]
    public async Task InvalidOwnerDoesNotPersistAnOrphanOrganization()
    {
        var repository = new MemoryProvisioning();
        await Assert.ThrowsAsync<ArgumentException>(() => new ProvisionOrganizationService(repository, new FakePasswords()).ExecuteAsync(
            new("norte", "Norte", "owner", "Owner", "", "+573001234567"), default));
        Assert.Equal(0, repository.Calls);
    }

    private sealed class MemoryProvisioning : IOrganizationProvisioningRepository
    {
        public bool Created { get; init; } = true;
        public int Calls { get; private set; }
        public Organization? Organization { get; private set; }
        public ApplicationUser? Owner { get; private set; }
        public OrganizationMembership? Membership { get; private set; }
        public AdministrativeAuditRecord? Audit { get; private set; }
        public Task<bool> TryCreateAsync(Organization organization, ApplicationUser owner, OrganizationMembership membership, AdministrativeAuditRecord audit, CancellationToken cancellationToken)
        {
            Calls++; Organization = organization; Owner = owner; Membership = membership; Audit = audit;
            return Task.FromResult(Created);
        }
    }
    private sealed class FakePasswords : IPasswordCredentialService
    {
        public string Hash(string password) => "hash";
        public bool Verify(ApplicationUser user, string password) => false;
    }
}
