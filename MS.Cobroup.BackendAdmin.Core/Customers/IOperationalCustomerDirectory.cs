namespace MS.Cobroup.BackendAdmin.Core.Customers;

public interface IOperationalCustomerDirectory
{
    Task<CustomerProxyResult> SearchAsync(
        string accessToken,
        Guid? routeId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CustomerProxyResult> GetAsync(string accessToken, Guid customerId, CancellationToken cancellationToken);

    Task<CustomerProxyResult> CreateAsync(string accessToken, string json, CancellationToken cancellationToken);

    Task<CustomerProxyResult> UpdateAsync(
        string accessToken,
        Guid customerId,
        string json,
        CancellationToken cancellationToken);
}

public sealed record CustomerProxyResult(int StatusCode, string? Content, string ContentType)
{
    public static CustomerProxyResult Unavailable() => new(503, null, "application/problem+json");
}
