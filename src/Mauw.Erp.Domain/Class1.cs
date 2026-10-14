namespace Mauw.Erp.Domain;

public sealed class PricingEngine
{
    public PricingResult Calculate(
        decimal purchaseCost,
        decimal markupRate,
        decimal discountRate,
        decimal vatRate)
    {
        var listPrice = RoundCurrency(purchaseCost * (1 + (markupRate / 100m)));
        var finalNetPrice = RoundCurrency(listPrice * (1 - (discountRate / 100m)));
        var vatAmount = RoundCurrency(finalNetPrice * (vatRate / 100m));
        var finalGrossPrice = finalNetPrice + vatAmount;
        var marginAmount = finalNetPrice - purchaseCost;
        var marginPercent = finalNetPrice > 0
            ? RoundPercentage((marginAmount / finalNetPrice) * 100m)
            : 0m;

        return new PricingResult(
            listPrice,
            finalNetPrice,
            vatAmount,
            finalGrossPrice,
            marginAmount,
            marginPercent);
    }

    private static decimal RoundCurrency(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundPercentage(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record PricingResult(
    decimal ListPrice,
    decimal FinalNetPrice,
    decimal VatAmount,
    decimal FinalGrossPrice,
    decimal MarginAmount,
    decimal MarginPercent);
