using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// ProductVariant-specific repository operations.
/// </summary>
public interface IProductVariantRepository : IRepository<ProductVariant>
{
    Task<ProductVariant?> GetByBarcodeAsync(string barcode);
}