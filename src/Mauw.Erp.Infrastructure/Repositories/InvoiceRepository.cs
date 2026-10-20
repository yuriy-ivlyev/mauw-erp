using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Invoice repository implementation.
/// </summary>
public class InvoiceRepository(MauwDbContext dbContext) : Repository<Invoice, Guid>(dbContext), IInvoiceRepository
{
    public override async Task<Invoice?> GetByIdAsync(Guid id) =>
        await _dbSet
            .Include(i => i.Contact)
                .ThenInclude(c => c.Addresses)
            .Include(i => i.Items).ThenInclude(ii => ii.Variant).ThenInclude(v => v.Product)
            .Include(i => i.Items).ThenInclude(ii => ii.Variant).ThenInclude(v => v.Color)
            .Include(i => i.Items).ThenInclude(ii => ii.Variant).ThenInclude(v => v.Size)
            .Include(i => i.Payments)
            .Include(i => i.CreditNotes)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Invoice?> GetByNumberAsync(string invoiceNumber)
    {
        return await _dbSet
            .Include(i => i.Contact)
                .ThenInclude(c => c.Addresses)
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .Include(i => i.CreditNotes)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    public async Task<IEnumerable<Invoice>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(i => i.Contact)
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .AsNoTracking()
            .Where(i => i.ContactId == contactId)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<(IEnumerable<Invoice> Items, int Total)> GetByContactIdPagedAsync(
        Guid contactId,
        int page,
        int pageSize)
    {
        var query = _dbSet
            .Where(i => i.ContactId == contactId);

        var total = await query.CountAsync();
        var items = await query
            .Include(i => i.Contact)
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IEnumerable<Invoice>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds)
        => await _dbSet
            .Include(i => i.Payments)
            .Include(i => i.CreditNotes)
            .AsNoTracking()
            .Where(i => i.OrderId.HasValue && orderIds.Contains(i.OrderId.Value))
            .ToListAsync();

    public async Task<int> CountInvoicesByPrefixAsync(string prefix)
        => await _dbSet.CountAsync(i => i.InvoiceNumber.StartsWith(prefix));
}
