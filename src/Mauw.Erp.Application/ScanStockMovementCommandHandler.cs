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
        var variant = await _inventoryRepository.GetByBarcodeAsync(command.Barcode, cancellationToken)
            ?? throw new InvalidOperationException($"Variant with barcode '{command.Barcode}' was not found.");

        variant.ApplyStockMovement(command.Quantity);

        var stockMovement = new StockMovement(
            variant.Id,
            command.Type,
            command.Quantity,
            variant.UnitCost,
            command.ReferenceDoc);

        return await _inventoryRepository.SaveAsync(variant, stockMovement, cancellationToken);
    }
}
