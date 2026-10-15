namespace Mauw.Erp.Domain.Services;

/// <summary>
/// Italian Fiscal Pricing Engine implementing DPR 633/72 rules.
/// All calculations use commercial rounding (MidpointRounding.AwayFromZero).
/// </summary>
public class PricingEngine
{
    private const int DecimalPlaces = 2;
    private const MidpointRounding RoundingMode = MidpointRounding.AwayFromZero;

    /// <summary>
    /// Represents the complete pricing calculation for a product line.
    /// </summary>
    public record PricingResult(
        decimal ListPrice,
        decimal FinalNetPrice,
        decimal VatAmount,
        decimal FinalGrossPrice,
        decimal MarginAmount,
        decimal MarginPercent
    );

    /// <summary>
    /// Calculates pricing following Italian fiscal rules:
    /// 1. ListPrice = Round(PurchaseCost * (1 + MarkupRate / 100), 2)
    /// 2. FinalNetPrice = Round(ListPrice * (1 - DiscountRate / 100), 2)
    /// 3. VatAmount = Round(FinalNetPrice * (VatRate / 100), 2)
    /// 4. FinalGrossPrice = FinalNetPrice + VatAmount
    /// 5. MarginAmount = FinalNetPrice - PurchaseCost
    /// 6. MarginPercent = FinalNetPrice > 0 ? Round((MarginAmount / FinalNetPrice) * 100, 2) : 0
    /// </summary>
    public PricingResult CalculatePrice(
        decimal purchaseCost,
        decimal markupRate,
        decimal discountRate,
        decimal vatRate
    )
    {
        if (purchaseCost < 0 || markupRate < 0 || discountRate < 0 || vatRate < 0)
            throw new ArgumentException("All price components must be non-negative");

        if (markupRate > 100 || discountRate > 100)
            throw new ArgumentException("Markup and discount rates cannot exceed 100%");

        // Step 1: Calculate list price
        var listPrice = Round(purchaseCost * (1 + markupRate / 100), DecimalPlaces);

        // Step 2: Apply discount to get final net price
        var finalNetPrice = Round(listPrice * (1 - discountRate / 100), DecimalPlaces);

        // Step 3: Calculate VAT amount on net price
        var vatAmount = Round(finalNetPrice * (vatRate / 100), DecimalPlaces);

        // Step 4: Calculate gross price
        var finalGrossPrice = finalNetPrice + vatAmount;

        // Step 5: Calculate margin
        var marginAmount = finalNetPrice - purchaseCost;

        // Step 6: Calculate margin percent
        var marginPercent = finalNetPrice > 0
            ? Round((marginAmount / finalNetPrice) * 100, DecimalPlaces)
            : 0;

        return new(
            ListPrice: listPrice,
            FinalNetPrice: finalNetPrice,
            VatAmount: vatAmount,
            FinalGrossPrice: finalGrossPrice,
            MarginAmount: marginAmount,
            MarginPercent: marginPercent
        );
    }

    /// <summary>
    /// Represents VAT grouped by rate for Castelletto IVA calculation.
    /// </summary>
    public record VatBreakdown(
        decimal VatRate,
        decimal TaxableAmount,
        decimal VatAmount
    );

    /// <summary>
    /// Calculates VAT breakdown grouped by VAT rate (Castelletto IVA).
    /// For each rate: round the sum of taxable amounts first, then apply the rate.
    /// </summary>
    public IEnumerable<VatBreakdown> CalculateVatBreakdown(IEnumerable<(decimal TaxableAmount, decimal VatRate)> items)
    {
        var grouped = items
            .GroupBy(x => x.VatRate)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var sumTaxableAmounts = group.Sum(x => x.TaxableAmount);
            var roundedTaxableAmount = Round(sumTaxableAmounts, DecimalPlaces);
            var vatAmount = Round(roundedTaxableAmount * (group.Key / 100), DecimalPlaces);

            yield return new(
                VatRate: group.Key,
                TaxableAmount: roundedTaxableAmount,
                VatAmount: vatAmount
            );
        }
    }

    /// <summary>
    /// Commercial rounding using MidpointRounding.AwayFromZero.
    /// Ensures 12.455 € rounds to 12.46 € and 12.454 € rounds to 12.45 €.
    /// </summary>
    private static decimal Round(decimal value, int decimals)
    {
        return decimal.Round(value, decimals, RoundingMode);
    }
}
