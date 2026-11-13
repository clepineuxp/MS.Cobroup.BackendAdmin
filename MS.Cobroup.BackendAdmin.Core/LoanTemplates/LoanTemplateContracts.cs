using MS.Cobroup.BackendAdmin.Domain.LoanTemplates;

namespace MS.Cobroup.BackendAdmin.Core.LoanTemplates;

public sealed record LoanTemplateInput(
    string Name,
    string? Description,
    LoanTemplateClassification Classification,
    LoanTemplateStatus Status,
    int MaxLoanDays,
    decimal MaxPrincipalAmount,
    IReadOnlyCollection<decimal>? AllowedPercentages = null,
    decimal MinPrincipalAmount = 0.01m,
    decimal? MinPercentage = null,
    decimal? MaxPercentage = null,
    IReadOnlyCollection<Guid>? RouteIds = null);

public sealed record LoanTemplateSummary(
    Guid Id,
    string Name,
    string? Description,
    LoanTemplateClassification Classification,
    LoanTemplateStatus Status,
    int Version,
    int MaxLoanDays,
    decimal MaxPrincipalAmount,
    IReadOnlyCollection<decimal> AllowedPercentages,
    DateTimeOffset UpdatedAtUtc,
    decimal MinPrincipalAmount,
    decimal MinPercentage,
    decimal MaxPercentage,
    IReadOnlyCollection<Guid> RouteIds);

public sealed record LoanTemplateVersionSummary(
    int Version,
    string Name,
    string? Description,
    LoanTemplateClassification Classification,
    LoanTemplateStatus Status,
    int MaxLoanDays,
    decimal MaxPrincipalAmount,
    IReadOnlyCollection<decimal> AllowedPercentages,
    Guid RecordedByUserId,
    DateTimeOffset RecordedAtUtc,
    decimal MinPrincipalAmount,
    decimal MinPercentage,
    decimal MaxPercentage,
    IReadOnlyCollection<Guid> RouteIds);

public sealed record LoanTemplateDetails(
    Guid Id,
    string Name,
    string? Description,
    LoanTemplateClassification Classification,
    LoanTemplateStatus Status,
    int Version,
    int MaxLoanDays,
    decimal MaxPrincipalAmount,
    IReadOnlyCollection<decimal> AllowedPercentages,
    IReadOnlyCollection<LoanTemplateVersionSummary> Versions,
    Guid CreatedByUserId,
    Guid UpdatedByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    decimal MinPrincipalAmount,
    decimal MinPercentage,
    decimal MaxPercentage,
    IReadOnlyCollection<Guid> RouteIds);

public enum LoanTemplateOutcome
{
    Success,
    Created,
    Invalid,
    Forbidden,
    NotFound,
    Conflict
}

public sealed record LoanTemplateResult(
    LoanTemplateOutcome Outcome,
    LoanTemplateDetails? Template,
    string? Error)
{
    public static LoanTemplateResult Success(LoanTemplateDetails template) => new(LoanTemplateOutcome.Success, template, null);
    public static LoanTemplateResult Created(LoanTemplateDetails template) => new(LoanTemplateOutcome.Created, template, null);
    public static LoanTemplateResult Invalid(string error) => new(LoanTemplateOutcome.Invalid, null, error);
    public static LoanTemplateResult Forbidden() => new(LoanTemplateOutcome.Forbidden, null, null);
    public static LoanTemplateResult NotFound() => new(LoanTemplateOutcome.NotFound, null, null);
    public static LoanTemplateResult Conflict(string error) => new(LoanTemplateOutcome.Conflict, null, error);
}

public sealed record LoanTemplateListResult(
    LoanTemplateOutcome Outcome,
    IReadOnlyCollection<LoanTemplateSummary> Templates)
{
    public static LoanTemplateListResult Success(IReadOnlyCollection<LoanTemplateSummary> templates) =>
        new(LoanTemplateOutcome.Success, templates);

    public static LoanTemplateListResult Forbidden() => new(LoanTemplateOutcome.Forbidden, []);
}

public sealed record RouteLoanTemplateSummary(
    Guid Id,
    string Name,
    string? Description,
    LoanTemplateClassification Classification,
    LoanTemplateStatus Status,
    int MaxLoanDays,
    decimal MinPrincipalAmount,
    decimal MaxPrincipalAmount,
    decimal MinPercentage,
    decimal MaxPercentage,
    bool IsActiveForRoute);

public sealed record RouteLoanTemplatesResult(
    LoanTemplateOutcome Outcome,
    IReadOnlyCollection<RouteLoanTemplateSummary> Templates,
    string? Error)
{
    public static RouteLoanTemplatesResult Success(IReadOnlyCollection<RouteLoanTemplateSummary> templates) =>
        new(LoanTemplateOutcome.Success, templates, null);
    public static RouteLoanTemplatesResult Forbidden() => new(LoanTemplateOutcome.Forbidden, [], null);
    public static RouteLoanTemplatesResult NotFound(string error = "Ruta no encontrada.") => new(LoanTemplateOutcome.NotFound, [], error);
    public static RouteLoanTemplatesResult Invalid(string error) => new(LoanTemplateOutcome.Invalid, [], error);
    public static RouteLoanTemplatesResult Conflict(string error) => new(LoanTemplateOutcome.Conflict, [], error);
}
