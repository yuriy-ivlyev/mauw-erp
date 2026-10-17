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
    public async Task<IEnumerable<Order>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o => o.ContactId == contactId)
            .ToListAsync();
    }

    public async Task<int> CountCassaSessionsAsync() =>
        await _dbSet.CountAsync(o => o.OrderNumber.StartsWith("CASSA-"));

    public async Task<IEnumerable<Order>> GetSuspendedCassaSessionsAsync() =>
        await _dbSet
            .Include(o => o.Contact)
            .Where(o => o.Status == OrderStatus.Suspended && o.OrderNumber.StartsWith("CASSA-"))
            .OrderByDescending(o => o.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();

    public async Task<Order?> GetCassaSessionWithItemsAsync(Guid id) =>
        await _dbSet
            .Include(o => o.Contact)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Color)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Size)
            .FirstOrDefaultAsync(o => o.Id == id);
}