using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// StockMovement repository implementation.
/// </summary>
public class StockMovementRepository(MauwDbContext dbContext)
    : Repository<StockMovement, Guid>(dbContext), IStockMovementRepository
{
    public async Task<IEnumerable<StockMovement>> GetByVariantIdAsync(Guid variantId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(sm => sm.VariantId == variantId)
            .OrderByDescending(sm => sm.CreatedAt)
            .ToListAsync();
    }

    public async Task<(IEnumerable<StockMovement> Items, int Total)> GetByVariantIdPagedAsync(
        Guid variantId,
        int page,
        int pageSize)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(sm => sm.VariantId == variantId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(sm => sm.CreatedAt)
            .ThenByDescending(sm => sm.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}