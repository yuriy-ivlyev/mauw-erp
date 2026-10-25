using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// StockMovement-specific repository operations.
/// </summary>
public interface IStockMovementRepository : IRepository<StockMovement, Guid>
{
    Task<IEnumerable<StockMovement>> GetByVariantIdAsync(Guid variantId);
    Task<(IEnumerable<StockMovement> Items, int Total)> GetByVariantIdPagedAsync(Guid variantId, int page, int pageSize);
    Task<IEnumerable<StockMovement>> GetByReferenceIdAsync(Guid referenceId);
}