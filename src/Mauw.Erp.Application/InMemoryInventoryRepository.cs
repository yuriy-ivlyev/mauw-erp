using Mauw.Erp.Domain;

namespace Mauw.Erp.Application;

public class InMemoryInventoryRepository : IInventoryRepository
{
    public static readonly object SyncRoot = new();

    private readonly Dictionary<string, ProductVariant> _variants = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<StockMovement> _movements = new();

    public Task<ProductVariant?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        lock (SyncRoot)
        {
            _variants.TryGetValue(barcode, out var variant);
            return Task.FromResult(variant);
        }
    }

    public Task<ProductVariant> SaveAsync(ProductVariant variant, StockMovement stockMovement, CancellationToken cancellationToken = default)
    {
        lock (SyncRoot)
        {
            _variants[variant.Barcode] = variant;
            _movements.Add(stockMovement);
            return Task.FromResult(variant);
        }
    }

    public void Seed(ProductVariant variant)
    {
        lock (SyncRoot)
        {
            _variants[variant.Barcode] = variant;
        }
    }
}
