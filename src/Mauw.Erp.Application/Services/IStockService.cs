using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface IStockService
{
    Task<OperationResult<StockItemDto>> ExecuteMovementAsync(CreateStockMovementCommand command);
    Task<OperationResult<PagedResult<StockItemDto>>> GetStockSummaryPagedAsync(StockFilterRequest request);
    Task<OperationResult<PagedResult<StockMovementDetailDto>>> GetMovementsPagedAsync(Guid? warehouseId, int page, int pageSize);
    Task<OperationResult<bool>> TransferAsync(StockTransferCommand command);
    Task<OperationResult<decimal>> GetAvailableQtyAsync(Guid warehouseId, Guid variantId);
}
