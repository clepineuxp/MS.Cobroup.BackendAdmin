using MS.Cobroup.BackendAdmin.Core.LoanTemplates;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.LoanTemplates;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.LoanTemplates;

public sealed class LoanTemplateServiceTests
{
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid administratorId = Guid.NewGuid();

    [Fact]
    public async Task AdminCreatesTenantTemplateWithAudit()
    {
        var repository = new MemoryRepository();
        var service = new LoanTemplateService(repository);

        var result = await service.CreateAsync(Admin(), Input(), CancellationToken.None);

        Assert.Equal(LoanTemplateOutcome.Created, result.Outcome);
        Assert.Equal(organizationId, repository.Template?.OrganizationId);
        Assert.Equal(1, result.Template?.Version);
        Assert.Equal(repository.Template?.Id, repository.Audit?.EntityId);
        Assert.Contains("Credito diario", repository.Audit?.DetailsJson);
    }

    [Fact]
    public async Task NonAdminCannotCreateOrListAdministrativeCatalog()
    {
        var repository = new MemoryRepository();
        var service = new LoanTemplateService(repository);
        var collector = new TenantActor(Guid.NewGuid(), organizationId, UserRole.Collector);

        var creation = await service.CreateAsync(collector, Input(), CancellationToken.None);
        var listing = await service.ListAsync(collector, publishedOnly: false, CancellationToken.None);

        Assert.Equal(LoanTemplateOutcome.Forbidden, creation.Outcome);
        Assert.Equal(LoanTemplateOutcome.Forbidden, listing.Outcome);
        Assert.Null(repository.Template);
    }

    [Fact]
    public async Task AnyTenantRoleCanListOnlyPublishedTemplates()
    {
        var repository = new MemoryRepository();
        repository.Items.Add(CreatePublishedTemplate());
        var service = new LoanTemplateService(repository);
        var collector = new TenantActor(Guid.NewGuid(), organizationId, UserRole.Collector);

        var result = await service.ListAsync(collector, publishedOnly: true, CancellationToken.None);

        Assert.Equal(LoanTemplateOutcome.Success, result.Outcome);
        Assert.True(repository.LastPublishedOnly);
        Assert.Single(result.Templates);
    }

    [Fact]
    public async Task DuplicateNameIsRejectedBeforePersistence()
    {
        var repository = new MemoryRepository { NameExists = true };
        var service = new LoanTemplateService(repository);

        var result = await service.CreateAsync(Admin(), Input(), CancellationToken.None);

        Assert.Equal(LoanTemplateOutcome.Conflict, result.Outcome);
        Assert.Null(repository.Template);
    }

    private TenantActor Admin() => new(administratorId, organizationId, UserRole.Admin);

    private static LoanTemplateInput Input() => new(
        "Credito diario",
        "Plantilla base",
        LoanTemplateClassification.Low,
        LoanTemplateStatus.Published,
        30,
        1_000_000m,
        [5m, 10m]);

    private LoanTemplate CreatePublishedTemplate() => LoanTemplate.Create(
        organizationId,
        "Credito diario",
        null,
        LoanTemplateClassification.Low,
        LoanTemplateStatus.Published,
        30,
        1_000_000m,
        [5m],
        administratorId,
        DateTimeOffset.UtcNow);

    private sealed class MemoryRepository : ILoanTemplateRepository
    {
        public List<LoanTemplate> Items { get; } = [];
        public bool NameExists { get; init; }
        public bool LastPublishedOnly { get; private set; }
        public LoanTemplate? Template { get; private set; }
        public AdministrativeAuditRecord? Audit { get; private set; }

        public Task<IReadOnlyCollection<LoanTemplate>> ListAsync(Guid organizationId, bool publishedOnly, CancellationToken cancellationToken)
        {
            LastPublishedOnly = publishedOnly;
            IReadOnlyCollection<LoanTemplate> items = Items;
            return Task.FromResult(items);
        }

        public Task<LoanTemplate?> FindAsync(Guid organizationId, Guid templateId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == templateId));

        public Task<bool> NameExistsAsync(Guid organizationId, string normalizedName, Guid? excludedTemplateId, CancellationToken cancellationToken) =>
            Task.FromResult(NameExists);

        public Task<LoanTemplateSaveOutcome> TryAddAsync(LoanTemplate template, AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken)
        {
            Template = template;
            Audit = auditRecord;
            Items.Add(template);
            return Task.FromResult(LoanTemplateSaveOutcome.Saved);
        }

        public Task<LoanTemplateSaveOutcome> TrySaveAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken)
        {
            Audit = auditRecord;
            return Task.FromResult(LoanTemplateSaveOutcome.Saved);
        }
    }
}
