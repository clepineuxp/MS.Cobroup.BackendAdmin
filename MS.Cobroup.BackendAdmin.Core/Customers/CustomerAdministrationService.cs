using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Customers;

public sealed class CustomerAdministrationService(IOperationalCustomerDirectory directory)
{
    public Task<CustomerProxyResult> SearchAsync(
        TenantActor actor,
        string accessToken,
        Guid? routeId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.SearchAsync(accessToken, routeId, search, page, pageSize, cancellationToken)
            : Task.FromResult(new CustomerProxyResult(403, null, "application/problem+json"));

    public Task<CustomerProxyResult> GetAsync(
        TenantActor actor,
        string accessToken,
        Guid customerId,
        CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.GetAsync(accessToken, customerId, cancellationToken)
            : Task.FromResult(new CustomerProxyResult(403, null, "application/problem+json"));

    public Task<CustomerProxyResult> CreateAsync(
        TenantActor actor,
        string accessToken,
        string json,
        CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.CreateAsync(accessToken, json, cancellationToken)
            : Task.FromResult(new CustomerProxyResult(403, null, "application/problem+json"));

    public Task<CustomerProxyResult> UpdateAsync(
        TenantActor actor,
        string accessToken,
        Guid customerId,
        string json,
        CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.UpdateAsync(accessToken, customerId, json, cancellationToken)
            : Task.FromResult(new CustomerProxyResult(403, null, "application/problem+json"));
}
