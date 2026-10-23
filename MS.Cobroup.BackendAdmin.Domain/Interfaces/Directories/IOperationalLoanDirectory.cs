namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Directories;

public interface IOperationalLoanDirectory
{
    Task<LoanProxyResult> SearchAsync(
        string accessToken,
        Guid? routeId,
        Guid? customerId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<LoanProxyResult> GetAsync(
        string accessToken,
        Guid loanId,
        CancellationToken cancellationToken);

    Task<LoanProxyResult> ListByCustomerAsync(
        string accessToken,
        Guid customerId,
        CancellationToken cancellationToken);
}

public sealed record LoanProxyResult(int StatusCode, string? Content, string ContentType)
{
    public static LoanProxyResult Unavailable() => new(503, null, "application/problem+json");
}
