using MS.Cobroup.BackendAdmin.Core.PaymentMethods;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.PaymentMethods;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using Xunit;

namespace MS.Cobroup.BackendAdmin.Core.Tests.PaymentMethods;

public sealed class PaymentMethodServiceTests
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _collectorId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_WhenActorIsNotAdmin_ReturnsForbidden()
    {
        var repo = new InMemoryPaymentMethodRepository();
        var service = new PaymentMethodService(repo);
        var actor = new TenantActor(_collectorId, _orgId, UserRole.Collector);

        var result = await service.CreateAsync(actor, new CreatePaymentMethodInput("Efectivo", 1), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PaymentMethodErrorType.Forbidden, result.ErrorType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Nombre excesivamente largo que sobrepasa los cincuenta caracteres reglamentarios")]
    public async Task CreateAsync_WithInvalidName_ReturnsValidationError(string name)
    {
        var repo = new InMemoryPaymentMethodRepository();
        var service = new PaymentMethodService(repo);
        var actor = new TenantActor(_adminId, _orgId, UserRole.Admin);

        var result = await service.CreateAsync(actor, new CreatePaymentMethodInput(name, 1), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PaymentMethodErrorType.ValidationError, result.ErrorType);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateName_ReturnsDuplicateNameError()
    {
        var repo = new InMemoryPaymentMethodRepository();
        var service = new PaymentMethodService(repo);
        var actor = new TenantActor(_adminId, _orgId, UserRole.Admin);

        await service.CreateAsync(actor, new CreatePaymentMethodInput("Efectivo", 1), CancellationToken.None);
        var result = await service.CreateAsync(actor, new CreatePaymentMethodInput("EFECTIVO", 2), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PaymentMethodErrorType.DuplicateName, result.ErrorType);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_SavesEntityAndAudit()
    {
        var repo = new InMemoryPaymentMethodRepository();
        var service = new PaymentMethodService(repo);
        var actor = new TenantActor(_adminId, _orgId, UserRole.Admin);

        var result = await service.CreateAsync(actor, new CreatePaymentMethodInput("Transferencia Bancolombia", 1), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Item);
        Assert.Equal("Transferencia Bancolombia", result.Item.Name);
        Assert.True(result.Item.IsActive);
        Assert.NotNull(repo.LastAudit);
        Assert.Equal("payment_method.created", repo.LastAudit.EventType);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesPaymentMethodSuccessfully()
    {
        var repo = new InMemoryPaymentMethodRepository();
        var service = new PaymentMethodService(repo);
        var actor = new TenantActor(_adminId, _orgId, UserRole.Admin);

        var created = await service.CreateAsync(actor, new CreatePaymentMethodInput("Nequi", 1), CancellationToken.None);
        var updated = await service.UpdateAsync(actor, created.Item!.Id, new UpdatePaymentMethodInput("Nequi / Daviplata", 5), CancellationToken.None);

        Assert.True(updated.Success);
        Assert.Equal("Nequi / Daviplata", updated.Item!.Name);
        Assert.Equal(5, updated.Item.DisplayOrder);
        Assert.Equal("payment_method.updated", repo.LastAudit?.EventType);
    }

    [Fact]
    public async Task SetStatusAsync_TogglesActiveState()
    {
        var repo = new InMemoryPaymentMethodRepository();
        var service = new PaymentMethodService(repo);
        var actor = new TenantActor(_adminId, _orgId, UserRole.Admin);

        var created = await service.CreateAsync(actor, new CreatePaymentMethodInput("Cheque", 10), CancellationToken.None);
        var disabled = await service.SetStatusAsync(actor, created.Item!.Id, false, CancellationToken.None);

        Assert.True(disabled.Success);
        Assert.False(disabled.Item!.IsActive);
        Assert.Equal("payment_method.status_changed", repo.LastAudit?.EventType);
    }

    [Fact]
    public async Task ListAvailableAsync_ReturnsOnlyActivePaymentMethods()
    {
        var repo = new InMemoryPaymentMethodRepository();
        var service = new PaymentMethodService(repo);
        var actor = new TenantActor(_adminId, _orgId, UserRole.Admin);

        var p1 = await service.CreateAsync(actor, new CreatePaymentMethodInput("Efectivo", 1), CancellationToken.None);
        var p2 = await service.CreateAsync(actor, new CreatePaymentMethodInput("Desactivado", 2), CancellationToken.None);
        await service.SetStatusAsync(actor, p2.Item!.Id, false, CancellationToken.None);

        var available = await service.ListAvailableAsync(_orgId, CancellationToken.None);

        Assert.Single(available);
        Assert.Equal("Efectivo", available.First().Name);
    }

    private sealed class InMemoryPaymentMethodRepository : IPaymentMethodRepository
    {
        private readonly List<PaymentMethod> _items = [];
        public AdministrativeAuditRecord? LastAudit { get; private set; }

        public Task<IReadOnlyCollection<PaymentMethod>> ListAsync(
            Guid organizationId,
            bool activeOnly,
            CancellationToken cancellationToken)
        {
            var query = _items.Where(x => x.OrganizationId == organizationId);
            if (activeOnly) query = query.Where(x => x.IsActive);
            return Task.FromResult<IReadOnlyCollection<PaymentMethod>>(query.OrderBy(x => x.DisplayOrder).ToList());
        }

        public Task<PaymentMethod?> FindAsync(
            Guid organizationId,
            Guid paymentMethodId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_items.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == paymentMethodId));

        public Task<bool> NameExistsAsync(
            Guid organizationId,
            string normalizedName,
            Guid? excludedId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_items.Any(x => x.OrganizationId == organizationId && x.NormalizedName == normalizedName && (!excludedId.HasValue || x.Id != excludedId.Value)));

        public Task<PaymentMethodSaveOutcome> TryAddAsync(
            PaymentMethod paymentMethod,
            AdministrativeAuditRecord auditRecord,
            CancellationToken cancellationToken)
        {
            if (_items.Any(x => x.OrganizationId == paymentMethod.OrganizationId && x.NormalizedName == paymentMethod.NormalizedName))
            {
                return Task.FromResult(PaymentMethodSaveOutcome.DuplicateName);
            }

            _items.Add(paymentMethod);
            LastAudit = auditRecord;
            return Task.FromResult(PaymentMethodSaveOutcome.Saved);
        }

        public Task<PaymentMethodSaveOutcome> TrySaveAsync(
            AdministrativeAuditRecord auditRecord,
            CancellationToken cancellationToken)
        {
            LastAudit = auditRecord;
            return Task.FromResult(PaymentMethodSaveOutcome.Saved);
        }
    }
}
