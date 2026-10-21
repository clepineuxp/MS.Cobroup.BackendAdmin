using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Loans;

public sealed class LoanAdministrationService(IOperationalLoanDirectory directory)
{
    public Task<LoanProxyResult> SearchAsync(
        TenantActor actor,
        string accessToken,
        Guid? routeId,
        Guid? customerId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.SearchAsync(accessToken, routeId, customerId, status, page, pageSize, cancellationToken)
            : Task.FromResult(new LoanProxyResult(403, null, "application/problem+json"));

    public Task<LoanProxyResult> GetAsync(
        TenantActor actor,
        string accessToken,
        Guid loanId,
        CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.GetAsync(accessToken, loanId, cancellationToken)
            : Task.FromResult(new LoanProxyResult(403, null, "application/problem+json"));

    public Task<LoanProxyResult> ListByCustomerAsync(
        TenantActor actor,
        string accessToken,
        Guid customerId,
        CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.ListByCustomerAsync(accessToken, customerId, cancellationToken)
            : Task.FromResult(new LoanProxyResult(403, null, "application/problem+json"));
}
