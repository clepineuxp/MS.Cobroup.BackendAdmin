namespace MS.Cobroup.BackendAdmin.Core.LoanExceptions;

public interface IOperationalLoanExceptionDirectory
{
    Task<LoanExceptionProxyResult> SearchAsync(
        string accessToken,
        Guid? routeId,
        Guid? customerId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<LoanExceptionProxyResult> GetAsync(
        string accessToken,
        Guid exceptionId,
        CancellationToken cancellationToken);

    Task<LoanExceptionProxyResult> ApplyDecisionAsync(
        string accessToken,
        Guid exceptionId,
        string json,
        CancellationToken cancellationToken);
}

public sealed record LoanExceptionProxyResult(int StatusCode, string? Content, string ContentType)
{
    public static LoanExceptionProxyResult Unavailable() => new(503, null, "application/problem+json");
}
