using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface IStockRepository
{
    Task<StockItem?> GetStockItemAsync(Guid warehouseId, Guid productVariantId);
    Task<StockItem> GetOrCreateStockItemAsync(Guid warehouseId, Guid productVariantId);
    Task<(IEnumerable<StockItem> Items, int Total)> GetStockByWarehousePagedAsync(
        Guid? warehouseId, string? search, int page, int pageSize, string? sortBy = null, bool sortDescending = false);
    Task<(IEnumerable<StockMovement> Items, int Total)> GetMovementsPagedAsync(
        Guid? warehouseId, int page, int pageSize, string? sortBy = null, bool sortDescending = false);
    Task RecordMovementAsync(StockMovement movement, decimal deltaOnHand);
    Task<decimal> GetAvailableQtyAsync(Guid warehouseId, Guid productVariantId);
}
