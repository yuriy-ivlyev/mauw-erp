using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Decorators;

public sealed class TracingInventoryService(IInventoryService inner) : TracingDecoratorBase, IInventoryService
{
    public Task<OperationResult<StockMovementDto>> ScanBarcodeAsync(BarcodeScanRequest request) =>
        TraceAsync("Inventory.ScanBarcode", () => inner.ScanBarcodeAsync(request),
            a => a?.SetTag("barcode", request.Barcode));

    public Task<OperationResult<PagedResult<StockMovementDto>>> GetMovementsAsync(Guid variantId, int page, int pageSize) =>
        TraceAsync("Inventory.GetMovements", () => inner.GetMovementsAsync(variantId, page, pageSize),
            a => a?.SetTag("variant.id", variantId));

    public Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode) =>
        TraceAsync("Inventory.GetVariantByBarcode", () => inner.GetVariantByBarcodeAsync(barcode),
            a => a?.SetTag("barcode", barcode));
}
