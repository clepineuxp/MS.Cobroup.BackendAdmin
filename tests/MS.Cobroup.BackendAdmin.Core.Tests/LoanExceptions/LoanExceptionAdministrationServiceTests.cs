using MS.Cobroup.BackendAdmin.Core.LoanExceptions;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Tests.LoanExceptions;

public sealed class LoanExceptionAdministrationServiceTests
{
    [Fact]
    public async Task Admin_requests_are_forwarded_to_operational_directory()
    {
        var directory = new FakeDirectory();
        var auditRepo = new FakeAuditRepository();
        var actor = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);

        var service = new LoanExceptionAdministrationService(directory, auditRepo);
        var result = await service.SearchAsync(actor, "token-123", null, null, null, 1, 25, default);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("token-123", directory.Token);
    }

    [Fact]
    public async Task Non_admin_requests_are_rejected_in_core_with_forbidden()
    {
        var directory = new FakeDirectory();
        var auditRepo = new FakeAuditRepository();
        var supervisor = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Supervisor);
        var collector = new TenantActor(Guid.NewGuid(), Guid.NewGuid(), UserRole.Collector);

        var service = new LoanExceptionAdministrationService(directory, auditRepo);

        var result1 = await service.GetAsync(supervisor, "token", Guid.NewGuid(), default);
        var result2 = await service.ApplyDecisionAsync(collector, "token", Guid.NewGuid(), "{}", default);

        Assert.Equal(403, result1.StatusCode);
        Assert.Equal(403, result2.StatusCode);
        Assert.Null(directory.Token);
        Assert.Empty(auditRepo.Records);
    }

    [Fact]
    public async Task Successful_decision_records_administrative_audit()
    {
        var directory = new FakeDirectory();
        var auditRepo = new FakeAuditRepository();
        var organizationId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var exceptionId = Guid.NewGuid();
        var actor = new TenantActor(adminId, organizationId, UserRole.Admin);

        var service = new LoanExceptionAdministrationService(directory, auditRepo);
        var decisionJson = "{\"decision\":\"Approved\",\"decisionNotes\":\"Autorizado por administración\"}";

        var result = await service.ApplyDecisionAsync(actor, "token-admin", exceptionId, decisionJson, default);

        Assert.Equal(200, result.StatusCode);
        Assert.Single(auditRepo.Records);
        var record = auditRepo.Records[0];
        Assert.Equal(organizationId, record.OrganizationId);
        Assert.Equal(adminId, record.ActorUserId);
        Assert.Equal(exceptionId, record.EntityId);
        Assert.Equal("loan_exception.approved", record.EventType);
        Assert.Equal("loan_exception", record.EntityType);
    }

    private sealed class FakeDirectory : IOperationalLoanExceptionDirectory
    {
        public string? Token { get; private set; }

        public Task<LoanExceptionProxyResult> SearchAsync(
            string accessToken,
            Guid? routeId,
            Guid? customerId,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Token = accessToken;
            return Task.FromResult(new LoanExceptionProxyResult(200, "{}", "application/json"));
        }

        public Task<LoanExceptionProxyResult> GetAsync(string accessToken, Guid exceptionId, CancellationToken cancellationToken)
        {
            Token = accessToken;
            return Task.FromResult(new LoanExceptionProxyResult(200, "{}", "application/json"));
        }

        public Task<LoanExceptionProxyResult> ApplyDecisionAsync(string accessToken, Guid exceptionId, string json, CancellationToken cancellationToken)
        {
            Token = accessToken;
            return Task.FromResult(new LoanExceptionProxyResult(200, "{\"status\":\"Approved\"}", "application/json"));
        }
    }

    private sealed class FakeAuditRepository : ILoanExceptionAuditRepository
    {
        public List<AdministrativeAuditRecord> Records { get; } = [];

        public Task AddAuditAsync(AdministrativeAuditRecord auditRecord, CancellationToken cancellationToken)
        {
            Records.Add(auditRecord);
            return Task.CompletedTask;
        }
    }
}
