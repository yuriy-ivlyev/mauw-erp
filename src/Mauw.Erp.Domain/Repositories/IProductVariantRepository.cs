using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// ProductVariant-specific repository operations.
/// </summary>
public interface IProductVariantRepository : IRepository<ProductVariant, Guid>
{
    Task<ProductVariant?> GetByBarcodeAsync(string barcode);

    /// <summary>
    /// Ricerca full-text varianti per barcode, nome prodotto, colore o taglia.
    /// </summary>
    Task<IEnumerable<ProductVariant>> SearchAsync(string query, int limit = 10);
}