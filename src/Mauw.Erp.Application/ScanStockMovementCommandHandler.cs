using Mauw.Erp.Domain;

namespace Mauw.Erp.Application;

public class ScanStockMovementCommandHandler
{
    private readonly IInventoryRepository _inventoryRepository;

    public ScanStockMovementCommandHandler(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public async Task<ProductVariant> HandleAsync(ScanStockMovementCommand command, CancellationToken cancellationToken = default)
    {
        ProductVariant? variant;
        lock (InMemoryInventoryRepository.SyncRoot)
        {
            variant = _inventoryRepository.GetByBarcodeAsync(command.Barcode, cancellationToken).Result
                ?? throw new InvalidOperationException($"Variant with barcode '{command.Barcode}' was not found.");

            variant.ApplyStockMovement(command.Quantity);

            var stockMovement = new StockMovement(
                variant.Id,
                command.Type,
                command.Quantity,
                variant.UnitCost,
                command.ReferenceDoc);

            _inventoryRepository.SaveAsync(variant, stockMovement, cancellationToken).GetAwaiter().GetResult();
        }

        return variant;
    }
}
