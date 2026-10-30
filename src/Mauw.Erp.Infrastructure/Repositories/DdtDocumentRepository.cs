using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// DdtDocument repository implementation.
/// </summary>
public class DdtDocumentRepository(MauwDbContext dbContext) : Repository<DdtDocument, Guid>(dbContext), IDdtDocumentRepository
{
    public async Task<IEnumerable<DdtDocument>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(d => d.Contact)
            .Include(d => d.Items)
            .AsNoTracking()
            .Where(d => d.ContactId == contactId)
            .OrderByDescending(d => d.DdtDate)
            .ToListAsync();
    }

    public async Task<(IEnumerable<DdtDocument> Items, int Total)> GetByContactIdPagedAsync(
        Guid contactId,
        int page,
        int pageSize)
    {
        var query = _dbSet.Where(d => d.ContactId == contactId);
        var total = await query.CountAsync();
        var items = await query
            .Include(d => d.Contact)
            .Include(d => d.Items)
            .AsNoTracking()
            .OrderByDescending(d => d.DdtDate)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<DdtDocument?> GetByNumberAsync(string ddtNumber)
    {
        return await _dbSet
            .Include(d => d.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DdtNumber == ddtNumber);
    }

    public async Task<IEnumerable<DdtDocument>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds)
        => await _dbSet
            .AsNoTracking()
            .Include(d => d.Items)
            .Where(d => d.OrderId.HasValue && orderIds.Contains(d.OrderId.Value))
            .ToListAsync();

    public async Task<IEnumerable<DdtDocument>> GetByOrderIdWithItemsAsync(Guid orderId)
        => await _dbSet
            .AsNoTracking()
            .Include(d => d.Items)
            .Where(d => d.OrderId == orderId)
            .OrderBy(d => d.DdtDate)
            .ThenBy(d => d.Id)
            .ToListAsync();

    public async Task<int> CountDdtsByPrefixAsync(string prefix)
        => await _dbSet.CountAsync(d => d.DdtNumber.StartsWith(prefix));

    public async Task<long> GetNextShipmentSequenceAsync() =>
        await _dbContext.Database
            .SqlQueryRaw<long>("SELECT nextval('shipment_document_number_seq') AS \"Value\"")
            .SingleAsync();

    public override async Task<DdtDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(d => d.Contact)
                .ThenInclude(c => c.Addresses)
            .Include(d => d.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .Include(d => d.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Color)
            .Include(d => d.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Size)
                .Include(d => d.Shipment)
                .FirstOrDefaultAsync(d => d.Id == id);
    }
}
