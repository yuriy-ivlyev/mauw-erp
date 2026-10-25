using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Services;

public sealed class CatalogPriceCalculator
{
    public AccountingResult<DiscountedUnitPrice> GetStoredPrice(ProductVariant variant) =>
        Validate(variant.SellingPrice);

    public AccountingResult<DiscountedUnitPrice> Validate(decimal? sellingPrice)
    {
        if (!sellingPrice.HasValue)
            return AccountingResult<DiscountedUnitPrice>.Fail("Prezzo di vendita netto mancante: completare il catalogo.");
        if (sellingPrice < 0m || sellingPrice > 99999999999999.99999999m ||
            decimal.Round(sellingPrice.Value, 8, MidpointRounding.AwayFromZero) != sellingPrice)
            return AccountingResult<DiscountedUnitPrice>.Fail("Il prezzo di vendita netto deve essere non negativo e avere massimo 8 decimali.");
        return AccountingResult<DiscountedUnitPrice>.Ok(new(sellingPrice.Value));
    }

}
