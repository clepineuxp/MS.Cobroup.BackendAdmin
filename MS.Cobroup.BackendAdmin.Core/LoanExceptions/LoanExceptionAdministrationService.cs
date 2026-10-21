using System.Text.Json;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.LoanExceptions;

public sealed class LoanExceptionAdministrationService(
    IOperationalLoanExceptionDirectory directory,
    ILoanExceptionAuditRepository auditRepository)
{
    public Task<LoanExceptionProxyResult> SearchAsync(
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
            : Task.FromResult(new LoanExceptionProxyResult(403, null, "application/problem+json"));

    public Task<LoanExceptionProxyResult> GetAsync(
        TenantActor actor,
        string accessToken,
        Guid exceptionId,
        CancellationToken cancellationToken) =>
        actor.Role is UserRole.Admin
            ? directory.GetAsync(accessToken, exceptionId, cancellationToken)
            : Task.FromResult(new LoanExceptionProxyResult(403, null, "application/problem+json"));

    public async Task<LoanExceptionProxyResult> ApplyDecisionAsync(
        TenantActor actor,
        string accessToken,
        Guid exceptionId,
        string json,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return new LoanExceptionProxyResult(403, null, "application/problem+json");
        }

        var result = await directory.ApplyDecisionAsync(accessToken, exceptionId, json, cancellationToken);

        if (result.StatusCode == 200)
        {
            var decision = "Decided";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("decision", out var decisionProp) ||
                    doc.RootElement.TryGetProperty("Decision", out decisionProp))
                {
                    decision = decisionProp.GetString() ?? "Decided";
                }
            }
            catch
            {
                // Fallback
            }

            var audit = AdministrativeAuditRecord.LoanExceptionDecided(
                actor.OrganizationId,
                actor.UserId,
                exceptionId,
                decision,
                DateTimeOffset.UtcNow,
                json);

            await auditRepository.AddAuditAsync(audit, cancellationToken);
        }

        return result;
    }
}
