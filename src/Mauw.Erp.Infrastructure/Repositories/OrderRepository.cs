using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Order repository implementation.
/// </summary>
public class OrderRepository(MauwDbContext dbContext) : Repository<Order, Guid>(dbContext), IOrderRepository
{
    public override async Task<Order?> GetByIdAsync(Guid id) =>
        await _dbSet
            .Include(o => o.Contact)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<IEnumerable<Order>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(o => o.Contact)
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o => o.ContactId == contactId)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Order> Items, int Total)> GetByContactIdPagedAsync(
        Guid contactId,
        int page,
        int pageSize)
    {
        var query = _dbSet.Where(o => o.ContactId == contactId);
        var total = await query.CountAsync();
        var items = await query
            .Include(o => o.Contact)
            .Include(o => o.Items)
            .AsNoTracking()
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<int> CountCassaSessionsAsync() =>
        await _dbSet.CountAsync(o => o.OrderType == OrderType.CassaSession);

    public async Task<IEnumerable<Order>> GetSuspendedCassaSessionsAsync() =>
        await _dbSet
            .Include(o => o.Contact)
            .Where(o => o.Status == OrderStatus.Suspended && o.OrderType == OrderType.CassaSession)
            .OrderByDescending(o => o.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();

    public async Task<Order?> GetCassaSessionWithItemsAsync(Guid id) =>
        await _dbSet
            .Include(o => o.Contact)
                .ThenInclude(c => c.Addresses)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Color)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Size)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<(IEnumerable<Order> Items, int Total)> GetAllPagedAsync(
        string? search, string? status, string? orderType, int page, int pageSize, string? sortBy = null, bool sortDescending = false)
    {
        IQueryable<Order> query = _dbSet
            .Include(o => o.Contact)
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(o =>
                o.OrderNumber.Contains(search) ||
                o.Contact.BusinessName.Contains(search));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, out var statusEnum))
            query = query.Where(o => o.Status == statusEnum);

        if (!string.IsNullOrWhiteSpace(orderType) && Enum.TryParse<OrderType>(orderType, out var typeEnum))
            query = query.Where(o => o.OrderType == typeEnum);

        var total = await query.CountAsync();
        var orderedQuery = sortBy?.ToLowerInvariant() switch
        {
            "ordernumber" => sortDescending ? query.OrderByDescending(o => o.OrderNumber) : query.OrderBy(o => o.OrderNumber),
            "contactname" => sortDescending ? query.OrderByDescending(o => o.Contact.BusinessName) : query.OrderBy(o => o.Contact.BusinessName),
            "status" => sortDescending ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
            "taxabletotal" => sortDescending ? query.OrderByDescending(o => o.TaxableTotal) : query.OrderBy(o => o.TaxableTotal),
            "grosstotal" => sortDescending ? query.OrderByDescending(o => o.GrossTotal) : query.OrderBy(o => o.GrossTotal),
            "orderdate" => sortDescending ? query.OrderByDescending(o => o.OrderDate) : query.OrderBy(o => o.OrderDate),
            _ => string.IsNullOrWhiteSpace(sortBy)
                ? query.OrderByDescending(o => o.OrderDate)
                : query.OrderByDescending(o => o.OrderDate)
        };
        var items = await orderedQuery
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, total);
    }

    public async Task<int> CountOrdersByPrefixAsync(string prefix) =>
        await _dbSet.CountAsync(o => o.OrderNumber.StartsWith(prefix));
}
