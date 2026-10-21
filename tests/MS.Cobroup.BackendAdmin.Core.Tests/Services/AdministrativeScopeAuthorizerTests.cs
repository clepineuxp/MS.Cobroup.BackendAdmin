using MS.Cobroup.BackendAdmin.Core.Services;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Services;

public sealed class AdministrativeScopeAuthorizerTests
{
    private readonly AdministrativeScopeAuthorizer _authorizer = new();

    [Fact]
    public void CanAdministerTenant_AllowsAdminInOwnTenant()
    {
        var tenantId = Guid.NewGuid();
        var actor = CreateActor(tenantId, UserRole.Admin);

        Assert.True(_authorizer.CanAdministerTenant(actor, tenantId));
    }

    [Fact]
    public void CanAdministerTenant_DeniesSupervisor()
    {
        var tenantId = Guid.NewGuid();
        var actor = CreateActor(tenantId, UserRole.Supervisor);

        Assert.False(_authorizer.CanAdministerTenant(actor, tenantId));
    }

    [Fact]
    public void CanAdministerTenant_DeniesAnotherTenant()
    {
        var actor = CreateActor(Guid.NewGuid(), UserRole.Admin);

        Assert.False(_authorizer.CanAdministerTenant(actor, Guid.NewGuid()));
    }

    private static AuthenticatedActor CreateActor(Guid tenantId, UserRole role) => new(
        Guid.NewGuid(),
        tenantId,
        new HashSet<UserRole> { role },
        new HashSet<Guid>());
}
