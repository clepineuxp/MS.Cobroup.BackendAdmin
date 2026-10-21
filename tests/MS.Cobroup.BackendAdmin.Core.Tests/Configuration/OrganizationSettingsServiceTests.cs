using MS.Cobroup.BackendAdmin.Core.Configuration;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Configuration;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.Configuration;

public sealed class OrganizationSettingsServiceTests
{
    [Fact]
    public async Task AdminCreatesTenantSettingsWithAuditedUnlimitedDefaults()
    {
        var organizationId = Guid.NewGuid();
        var actor = new TenantActor(Guid.NewGuid(), organizationId, UserRole.Admin);
        var repository = new MemorySettingsRepository();
        var service = new OrganizationSettingsService(repository);

        var result = await service.UpdateAsync(
            new UpdateOrganizationSettingsCommand(actor, "UTC", "cop", null, 200),
            CancellationToken.None);

        Assert.Equal(OrganizationSettingsOutcome.Success, result.Outcome);
        Assert.Equal("COP", result.Settings?.CurrencyCode);
        Assert.Null(result.Settings?.MaxActiveCustomersPerRoute);
        Assert.Equal(organizationId, repository.Settings?.OrganizationId);
        Assert.Equal("organization.settings.updated", repository.Audit?.EventType);
    }

    [Fact]
    public async Task UnknownTimeZoneIsRejected()
    {
        var service = new OrganizationSettingsService(new MemorySettingsRepository());
        var result = await service.UpdateAsync(
            new UpdateOrganizationSettingsCommand(
                new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin),
                "Not/A-Time-Zone",
                "COP",
                100,
                100),
            CancellationToken.None);

        Assert.Equal(OrganizationSettingsOutcome.Invalid, result.Outcome);
    }

    private sealed class MemorySettingsRepository : IOrganizationSettingsRepository
    {
        public OrganizationSettings? Settings { get; private set; }
        public AdministrativeAuditRecord? Audit { get; private set; }

        public Task<OrganizationSettings?> FindAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Settings);

        public Task SaveAsync(OrganizationSettings settings, bool isNew, AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken)
        {
            Settings = settings;
            Audit = auditRecord;
            return Task.CompletedTask;
        }
    }
}
