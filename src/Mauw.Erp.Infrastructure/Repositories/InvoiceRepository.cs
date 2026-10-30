using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
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

    public async Task<Invoice?> GetByOrderIdAsync(Guid orderId) =>
        await _dbSet
            .Include(i => i.Contact).ThenInclude(contact => contact.Addresses)
            .Include(i => i.Items).ThenInclude(item => item.Variant).ThenInclude(variant => variant.Product)
            .Include(i => i.Items).ThenInclude(item => item.Variant).ThenInclude(variant => variant.Color)
            .Include(i => i.Items).ThenInclude(item => item.Variant).ThenInclude(variant => variant.Size)
            .Include(i => i.Payments)
            .Include(i => i.CreditNotes)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.OrderId == orderId);

    public async Task AcquireOrderInvoiceLockAsync(Guid orderId)
    {
        var lockKey = $"invoice-order:{orderId:N}";
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
    }

    public async Task AcquireInvoiceNumberLockAsync(string prefix)
    {
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({prefix}, 0))");
    }

    public async Task<List<string>> GetInvoiceNumbersByPrefixAsync(string prefix) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(invoice => invoice.InvoiceNumber.StartsWith(prefix))
            .Select(invoice => invoice.InvoiceNumber)
            .ToListAsync();

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
        int pageSize) =>
        await GetPagedAsync(contactId, null, page, pageSize);

    public async Task<(IEnumerable<Invoice> Items, int Total)> GetPagedAsync(
        Guid? contactId,
        string? search,
        int page,
        int pageSize,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        bool unpaidOnly = false,
        string? sortBy = null,
        bool sortDescending = true)
    {
        var query = _dbSet
            .Where(i => !contactId.HasValue || i.ContactId == contactId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i =>
                i.InvoiceNumber.Contains(term) ||
                i.Contact.BusinessName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status.Trim() switch
            {
                InvoiceStatuses.Draft => query.Where(i => !i.ConfirmedAt.HasValue && i.PaidAmount == 0m),
                InvoiceStatuses.Confirmed => query.Where(i => i.ConfirmedAt.HasValue && i.PaidAmount == 0m),
                InvoiceStatuses.PartiallyPaid => query.Where(i => i.PaidAmount > 0m && i.PaidAmount < i.GrossTotal),
                InvoiceStatuses.Paid => query.Where(i => i.GrossTotal > 0m && i.PaidAmount >= i.GrossTotal),
                _ => query.Where(_ => false)
            };
        }

        if (dateFrom.HasValue)
        {
            var start = dateFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(i => i.InvoiceDate >= start);
        }

        if (dateTo.HasValue)
        {
            var end = dateTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(i => i.InvoiceDate <= end);
        }

        if (unpaidOnly)
            query = query.Where(i => i.GrossTotal > i.PaidAmount);

        var total = await query.CountAsync();
        IOrderedQueryable<Invoice> orderedQuery = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("invoicenumber", false) => query.OrderBy(i => i.InvoiceNumber),
            ("invoicenumber", true) => query.OrderByDescending(i => i.InvoiceNumber),
            ("contactname", false) => query.OrderBy(i => i.Contact.BusinessName),
            ("contactname", true) => query.OrderByDescending(i => i.Contact.BusinessName),
            ("status", false) => query.OrderBy(i => i.InvoiceNumber),
            ("status", true) => query.OrderByDescending(i => i.InvoiceNumber),
            ("taxabletotal", false) => query.OrderBy(i => i.TaxableTotal),
            ("taxabletotal", true) => query.OrderByDescending(i => i.TaxableTotal),
            ("vattotal", false) => query.OrderBy(i => i.VatTotal),
            ("vattotal", true) => query.OrderByDescending(i => i.VatTotal),
            ("grosstotal", false) => query.OrderBy(i => i.GrossTotal),
            ("grosstotal", true) => query.OrderByDescending(i => i.GrossTotal),
            ("balancedue", false) => query.OrderBy(i => i.GrossTotal - i.PaidAmount),
            ("balancedue", true) => query.OrderByDescending(i => i.GrossTotal - i.PaidAmount),
            ("invoicedate", false) => query.OrderBy(i => i.InvoiceDate),
            ("invoicedate", true) => query.OrderByDescending(i => i.InvoiceDate),
            (_, _) => query.OrderByDescending(i => i.InvoiceDate)
        };
        var items = await orderedQuery
            .ThenByDescending(i => i.InvoiceNumber)
            .ThenByDescending(i => i.Id)
            .Include(i => i.Contact)
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .AsSplitQuery()
            .AsNoTracking()
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

}
