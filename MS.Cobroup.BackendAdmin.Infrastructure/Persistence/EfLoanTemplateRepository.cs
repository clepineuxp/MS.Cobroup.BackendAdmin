using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Core.LoanTemplates;
using MS.Cobroup.BackendAdmin.Domain.LoanTemplates;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfLoanTemplateRepository(IdentityDbContext dbContext) : ILoanTemplateRepository
{
    public async Task<IReadOnlyCollection<LoanTemplate>> ListAsync(
        Guid organizationId,
        bool publishedOnly,
        CancellationToken cancellationToken)
    {
        var query = dbContext.LoanTemplates
            .AsNoTracking()
            .Where(template => template.OrganizationId == organizationId);
        if (publishedOnly)
        {
            query = query.Where(template => template.Status == LoanTemplateStatus.Published);
        }

        return await query
            .OrderBy(template => template.Classification)
            .ThenBy(template => template.Name)
            .ToArrayAsync(cancellationToken);
    }

    public Task<LoanTemplate?> FindAsync(
        Guid organizationId,
        Guid templateId,
        CancellationToken cancellationToken) =>
        dbContext.LoanTemplates
            .Include(template => template.Versions)
            .SingleOrDefaultAsync(
                template => template.OrganizationId == organizationId && template.Id == templateId,
                cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludedTemplateId,
        CancellationToken cancellationToken) =>
        dbContext.LoanTemplates.AnyAsync(
            template => template.OrganizationId == organizationId
                && template.NormalizedName == normalizedName
                && (!excludedTemplateId.HasValue || template.Id != excludedTemplateId.Value),
            cancellationToken);

    public async Task<LoanTemplateSaveOutcome> TryAddAsync(
        LoanTemplate template,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.LoanTemplates.Add(template);
        dbContext.AdministrativeAuditRecords.Add(auditRecord);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LoanTemplateSaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (IsDuplicateName(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return LoanTemplateSaveOutcome.DuplicateName;
        }
    }

    public async Task<LoanTemplateSaveOutcome> TrySaveAsync(
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.AdministrativeAuditRecords.Add(auditRecord);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LoanTemplateSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LoanTemplateSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (IsDuplicateName(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return LoanTemplateSaveOutcome.DuplicateName;
        }
    }

    private static bool IsDuplicateName(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_loan_templates_OrganizationId_NormalizedName"
        };
}
