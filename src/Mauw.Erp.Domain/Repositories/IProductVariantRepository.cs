using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// ProductVariant-specific repository operations.
/// </summary>
public interface IProductVariantRepository : IRepository<ProductVariant, Guid>
{
    Task<ProductVariant?> GetByBarcodeAsync(string barcode);

    /// <summary>
    /// Ricerca varianti per codice, nome prodotto, colore o taglia; per gli articoli semplici usa il barcode del prodotto.
    /// </summary>
    Task<IEnumerable<ProductVariant>> SearchAsync(string query, int limit = 10, Guid? supplierId = null);
}