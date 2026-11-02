using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class CreditNoteRepository(MauwDbContext dbContext) : Repository<CreditNote, Guid>(dbContext), ICreditNoteRepository
{
    public override async Task<CreditNote?> GetByIdAsync(Guid id) =>
        await _dbSet
            .Include(note => note.Items).ThenInclude(item => item.Variant!).ThenInclude(variant => variant.Product)
            .Include(note => note.Items).ThenInclude(item => item.Variant!).ThenInclude(variant => variant.Color)
            .Include(note => note.Items).ThenInclude(item => item.Variant!).ThenInclude(variant => variant.Size)
            .AsSplitQuery()
            .FirstOrDefaultAsync(note => note.Id == id);

    public async Task<IEnumerable<CreditNote>> GetByInvoiceIdsAsync(IEnumerable<Guid> invoiceIds)
        => await _dbSet
            .AsNoTracking()
            .Include(note => note.Items)
            .Where(note => invoiceIds.Contains(note.InvoiceId))
            .ToListAsync();

    public Task<List<CreditNote>> GetForStatementByContactIdAsync(
        Guid contactId,
        CancellationToken cancellationToken = default) =>
        _dbSet
            .AsNoTracking()
            .Where(note => note.Invoice.ContactId == contactId && note.Invoice.ConfirmedAt.HasValue)
            .ToListAsync(cancellationToken);

    public Task<CreditNote?> GetForAllocationAsync(Guid creditNoteId) =>
        _dbSet
            .Include(note => note.Invoice)
            .FirstOrDefaultAsync(note => note.Id == creditNoteId);

    public Task<List<CreditNote>> GetAvailableForContactAsync(Guid contactId) =>
        _dbSet
            .AsNoTracking()
            .Include(note => note.Invoice)
            .Where(note => note.Invoice.ContactId == contactId && note.AllocatedAmount < note.GrossTotal)
            .OrderBy(note => note.CreditNoteDate)
            .ThenBy(note => note.CreditNoteNumber)
            .ToListAsync();

    public async Task<int> CountCreditNotesByPrefixAsync(string prefix)
        => await _dbSet.CountAsync(note => note.CreditNoteNumber.StartsWith(prefix));

    public async Task AcquireInvoiceCreditNoteLockAsync(Guid invoiceId)
    {
        var lockKey = $"credit-note-invoice:{invoiceId:N}";
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
    }

    public async Task AcquireCreditNoteAllocationLockAsync(Guid creditNoteId)
    {
        var lockKey = $"credit-note-allocation:{creditNoteId:N}";
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
    }

    public async Task AcquireSdiTransmissionLockAsync(Guid creditNoteId)
    {
        var lockKey = $"credit-note-sdi-transmission:{creditNoteId:N}";
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
    }

    public async Task AcquireCreditNoteNumberLockAsync(string prefix) =>
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({prefix}, 0))");

    public async Task<List<string>> GetCreditNoteNumbersByPrefixAsync(string prefix) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(note => note.CreditNoteNumber.StartsWith(prefix))
            .Select(note => note.CreditNoteNumber)
            .ToListAsync();
}
