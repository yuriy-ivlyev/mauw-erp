using Mauw.Erp.Domain;

namespace Mauw.Erp.Application;

public interface IInventoryRepository
{
    Task<ProductVariant?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<ProductVariant> SaveAsync(ProductVariant variant, StockMovement stockMovement, CancellationToken cancellationToken = default);
}
