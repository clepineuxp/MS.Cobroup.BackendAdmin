using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Core.PaymentMethods;
using MS.Cobroup.BackendAdmin.Domain.PaymentMethods;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using Npgsql;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class EfPaymentMethodRepository(IdentityDbContext dbContext) : IPaymentMethodRepository
{
    public async Task<IReadOnlyCollection<PaymentMethod>> ListAsync(
        Guid organizationId,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PaymentMethods
            .AsNoTracking()
            .Where(pm => pm.OrganizationId == organizationId);

        if (activeOnly)
        {
            query = query.Where(pm => pm.IsActive);
        }

        return await query
            .OrderBy(pm => pm.DisplayOrder)
            .ThenBy(pm => pm.Name)
            .ToArrayAsync(cancellationToken);
    }

    public Task<PaymentMethod?> FindAsync(
        Guid organizationId,
        Guid paymentMethodId,
        CancellationToken cancellationToken) =>
        dbContext.PaymentMethods
            .SingleOrDefaultAsync(
                pm => pm.OrganizationId == organizationId && pm.Id == paymentMethodId,
                cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.PaymentMethods.AnyAsync(
            pm => pm.OrganizationId == organizationId
                && pm.NormalizedName == normalizedName
                && (!excludedId.HasValue || pm.Id != excludedId.Value),
            cancellationToken);

    public async Task<PaymentMethodSaveOutcome> TryAddAsync(
        PaymentMethod paymentMethod,
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        var hasTransaction = dbContext.Database.CurrentTransaction is not null;
        var transaction = hasTransaction ? null : await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.PaymentMethods.Add(paymentMethod);
        dbContext.AdministrativeAuditRecords.Add(auditRecord);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return PaymentMethodSaveOutcome.Saved;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return PaymentMethodSaveOutcome.DuplicateName;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return PaymentMethodSaveOutcome.ConcurrencyConflict;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<PaymentMethodSaveOutcome> TrySaveAsync(
        AdministrativeAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        var hasTransaction = dbContext.Database.CurrentTransaction is not null;
        var transaction = hasTransaction ? null : await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.AdministrativeAuditRecords.Add(auditRecord);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return PaymentMethodSaveOutcome.Saved;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return PaymentMethodSaveOutcome.DuplicateName;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return PaymentMethodSaveOutcome.ConcurrencyConflict;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
