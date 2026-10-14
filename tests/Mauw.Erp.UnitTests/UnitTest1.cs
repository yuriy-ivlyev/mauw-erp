using Mauw.Erp.Application;
using Mauw.Erp.Domain;

namespace Mauw.Erp.UnitTests;

public class UnitTest1
{
    [Fact]
    public async Task ScanStockMovement_UpdatesOnHandQuantity_WhenReceivingAndShipping()
    {
        var repository = new InMemoryInventoryRepository();
        var variant = new ProductVariant(Guid.NewGuid(), "SKU-123", "Widget", 0, 10m);
        repository.Seed(variant);

        var handler = new ScanStockMovementCommandHandler(repository);

        await handler.HandleAsync(new ScanStockMovementCommand(variant.Barcode, 10, StockMovementType.PurchaseReceipt, "PO-1001"));
        await handler.HandleAsync(new ScanStockMovementCommand(variant.Barcode, -3, StockMovementType.SalesDelivery, "SO-2002"));

        Assert.Equal(7, variant.OnHandQuantity);
    }
}
