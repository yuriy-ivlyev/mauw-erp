using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Inventory application service - manages stock movements and barcode operations.
/// </summary>
public interface IInventoryService
{
    Task<OperationResult<StockMovementDto>> ScanBarcodeAsync(BarcodeScanRequest request);
    Task<OperationResult<PagedResult<StockMovementDto>>> GetMovementsAsync(Guid variantId, int page, int pageSize);
    Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode);
    Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeWithStockAsync(string barcode, Guid warehouseId);
}