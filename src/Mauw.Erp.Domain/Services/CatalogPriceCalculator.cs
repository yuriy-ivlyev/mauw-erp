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

    public AccountingResult<DiscountedUnitPrice> FromMarkup(decimal purchaseCost, decimal markup)
    {
        if (purchaseCost < 0m || markup is < 0m or > 9999m ||
            decimal.Round(markup, 2, MidpointRounding.AwayFromZero) != markup)
            return AccountingResult<DiscountedUnitPrice>.Fail("Costo o ricarico non valido: ricarico da 0 a 9999 con massimo 2 decimali.");
        try
        {
            return Validate(decimal.Round(purchaseCost * (1m + markup / 100m),
                8, MidpointRounding.AwayFromZero));
        }
        catch (OverflowException)
        {
            return AccountingResult<DiscountedUnitPrice>.Fail("Il calcolo del prezzo supera la capacita decimal.");
        }
    }
}
