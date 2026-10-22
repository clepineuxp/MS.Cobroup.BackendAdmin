using System.Text.Json;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.LoanTemplates;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.LoanTemplates;

public sealed class LoanTemplateService(ILoanTemplateRepository repository)
{
    public async Task<LoanTemplateListResult> ListAsync(
        TenantActor actor,
        bool publishedOnly,
        CancellationToken cancellationToken)
    {
        if (!publishedOnly && actor.Role is not UserRole.Admin)
        {
            return LoanTemplateListResult.Forbidden();
        }

        var templates = await repository.ListAsync(actor.OrganizationId, publishedOnly, cancellationToken);
        return LoanTemplateListResult.Success(templates.Select(ToSummary).ToArray());
    }

    public async Task<LoanTemplateResult> GetAsync(
        TenantActor actor,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return LoanTemplateResult.Forbidden();
        }

        var template = await repository.FindAsync(actor.OrganizationId, templateId, cancellationToken);
        return template is null
            ? LoanTemplateResult.NotFound()
            : LoanTemplateResult.Success(ToDetails(template));
    }

    public async Task<LoanTemplateResult> CreateAsync(
        TenantActor actor,
        LoanTemplateInput input,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return LoanTemplateResult.Forbidden();
        }

        LoanTemplate template;
        try
        {
            var normalizedName = LoanTemplate.NormalizeName(input.Name);
            if (await repository.NameExistsAsync(actor.OrganizationId, normalizedName, null, cancellationToken))
            {
                return LoanTemplateResult.Conflict("A loan template with this name already exists in the organization.");
            }

            var now = DateTimeOffset.UtcNow;
            template = LoanTemplate.Create(
                actor.OrganizationId,
                input.Name,
                input.Description,
                input.Classification,
                input.Status,
                input.MaxLoanDays,
                input.MaxPrincipalAmount,
                input.AllowedPercentages,
                actor.UserId,
                now);
            var audit = AdministrativeAuditRecord.LoanTemplateCreated(
                actor.OrganizationId,
                actor.UserId,
                template.Id,
                now,
                JsonSerializer.Serialize(new { After = Snapshot(template) }));
            var outcome = await repository.TryAddAsync(template, audit, cancellationToken);
            return outcome is LoanTemplateSaveOutcome.Saved
                ? LoanTemplateResult.Created(ToDetails(template))
                : LoanTemplateResult.Conflict("The loan template could not be created because its name already exists.");
        }
        catch (ArgumentException exception)
        {
            return LoanTemplateResult.Invalid(exception.Message);
        }
    }

    public async Task<LoanTemplateResult> UpdateAsync(
        TenantActor actor,
        Guid templateId,
        LoanTemplateInput input,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return LoanTemplateResult.Forbidden();
        }

        var template = await repository.FindAsync(actor.OrganizationId, templateId, cancellationToken);
        if (template is null)
        {
            return LoanTemplateResult.NotFound();
        }

        try
        {
            var normalizedName = LoanTemplate.NormalizeName(input.Name);
            if (await repository.NameExistsAsync(actor.OrganizationId, normalizedName, templateId, cancellationToken))
            {
                return LoanTemplateResult.Conflict("A loan template with this name already exists in the organization.");
            }

            var before = Snapshot(template);
            var now = DateTimeOffset.UtcNow;
            template.Update(
                input.Name,
                input.Description,
                input.Classification,
                input.Status,
                input.MaxLoanDays,
                input.MaxPrincipalAmount,
                input.AllowedPercentages,
                actor.UserId,
                now);
            var audit = AdministrativeAuditRecord.LoanTemplateUpdated(
                actor.OrganizationId,
                actor.UserId,
                template.Id,
                now,
                JsonSerializer.Serialize(new { Before = before, After = Snapshot(template) }));
            var outcome = await repository.TrySaveAsync(audit, cancellationToken);
            return outcome switch
            {
                LoanTemplateSaveOutcome.Saved => LoanTemplateResult.Success(ToDetails(template)),
                LoanTemplateSaveOutcome.DuplicateName => LoanTemplateResult.Conflict("A loan template with this name already exists in the organization."),
                _ => LoanTemplateResult.Conflict("The loan template changed concurrently. Reload it and try again.")
            };
        }
        catch (ArgumentException exception)
        {
            return LoanTemplateResult.Invalid(exception.Message);
        }
    }

    private static LoanTemplateSummary ToSummary(LoanTemplate template) => new(
        template.Id,
        template.Name,
        template.Description,
        template.Classification,
        template.Status,
        template.Version,
        template.MaxLoanDays,
        template.MaxPrincipalAmount,
        template.AllowedPercentages,
        template.UpdatedAtUtc);

    private static LoanTemplateDetails ToDetails(LoanTemplate template) => new(
        template.Id,
        template.Name,
        template.Description,
        template.Classification,
        template.Status,
        template.Version,
        template.MaxLoanDays,
        template.MaxPrincipalAmount,
        template.AllowedPercentages,
        template.Versions
            .OrderByDescending(version => version.Version)
            .Select(version => new LoanTemplateVersionSummary(
                version.Version,
                version.Name,
                version.Description,
                version.Classification,
                version.Status,
                version.MaxLoanDays,
                version.MaxPrincipalAmount,
                JsonSerializer.Deserialize<decimal[]>(version.AllowedPercentagesJson) ?? [],
                version.RecordedByUserId,
                version.RecordedAtUtc))
            .ToArray(),
        template.CreatedByUserId,
        template.UpdatedByUserId,
        template.CreatedAtUtc,
        template.UpdatedAtUtc);

    private static object Snapshot(LoanTemplate template) => new
    {
        template.Name,
        template.Description,
        template.Classification,
        template.Status,
        template.Version,
        template.MaxLoanDays,
        template.MaxPrincipalAmount,
        template.AllowedPercentages
    };
}
