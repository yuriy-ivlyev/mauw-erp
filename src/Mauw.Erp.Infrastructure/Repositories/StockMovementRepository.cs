using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// StockMovement repository implementation.
/// </summary>
public class StockMovementRepository(MauwDbContext dbContext)
    : Repository<StockMovement>(dbContext), IStockMovementRepository
{
    public async Task<IEnumerable<StockMovement>> GetByVariantIdAsync(Guid variantId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(sm => sm.VariantId == variantId)
            .OrderByDescending(sm => sm.CreatedAt)
            .ToListAsync();
    }
}