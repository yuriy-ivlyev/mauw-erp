using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Order repository implementation.
/// </summary>
public class OrderRepository(MauwDbContext dbContext) : Repository<Order>(dbContext), IOrderRepository
{
    public async Task<IEnumerable<Order>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o => o.ContactId == contactId)
            .ToListAsync();
    }
}