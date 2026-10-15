using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// StockMovement-specific repository operations.
/// </summary>
public interface IStockMovementRepository : IRepository<StockMovement, Guid>
{
    Task<IEnumerable<StockMovement>> GetByVariantIdAsync(Guid variantId);
}