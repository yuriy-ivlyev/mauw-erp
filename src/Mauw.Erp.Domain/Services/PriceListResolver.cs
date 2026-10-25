using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Domain.Services;

public sealed class PriceListResolver(ItalianAccountingCalculator calculator)
{
    public AccountingResult<ResolvedPrice> Resolve(PriceList? list, PriceResolutionInput input)
    {
        if (input.VariantId == Guid.Empty || input.ProductId == Guid.Empty ||
            input.Quantity <= 0m || input.BaseUnitPrice < 0m ||
            decimal.Round(input.Quantity, 8, MidpointRounding.AwayFromZero) != input.Quantity ||
            decimal.Round(input.BaseUnitPrice, 8, MidpointRounding.AwayFromZero) != input.BaseUnitPrice)
            return AccountingResult<ResolvedPrice>.Fail("Dati di risoluzione del prezzo non validi.");
        if (list is { IsActive: false })
            return AccountingResult<ResolvedPrice>.Fail("Il listino selezionato non e attivo.");

        var candidates = list?.Rules.Where(rule =>
            rule.MinimumQuantity <= input.Quantity &&
            (!rule.ValidFrom.HasValue || rule.ValidFrom <= input.At) &&
            (!rule.ValidTo.HasValue || rule.ValidTo >= input.At) &&
            (rule.VariantId.HasValue ? rule.VariantId == input.VariantId :
             rule.ProductId.HasValue ? rule.ProductId == input.ProductId :
             rule.CategoryId.HasValue ? rule.CategoryId == input.CategoryId : true))
            .OrderByDescending(Specificity).ThenByDescending(rule => rule.Priority).ToList() ?? [];

        var selected = candidates.FirstOrDefault();
        if (selected is not null && candidates.Skip(1).Any(rule =>
            Specificity(rule) == Specificity(selected) && rule.Priority == selected.Priority))
            return AccountingResult<ResolvedPrice>.Fail("Regole di listino ambigue: assegnare priorita differenti.");

        try
        {
            var unitPrice = selected?.CalculationType switch
            {
                PriceCalculationType.FixedPrice => selected.Value,
                _ => input.BaseUnitPrice
            };
            var discount = selected?.CalculationType == PriceCalculationType.PercentageDiscount
                ? selected.Value : 0m;
            if (unitPrice > 99999999999999.99999999m)
                return AccountingResult<ResolvedPrice>.Fail("Il prezzo supera la precisione supportata dalla persistenza.");
            var agreed = calculator.CalculateDiscountedUnitPrice(unitPrice, discount);
            var reason = discount > 0m
                ? selected?.Description ?? $"Sconto listino {list?.Name}".TrimEnd()
                : null;
            return agreed.Success
                ? AccountingResult<ResolvedPrice>.Ok(new ResolvedPrice(
                    list?.Id, selected?.Id, unitPrice, discount, agreed.Data!.Value, reason))
                : AccountingResult<ResolvedPrice>.Fail(agreed.Error);
        }
        catch (OverflowException)
        {
            return AccountingResult<ResolvedPrice>.Fail("Il calcolo del prezzo supera la capacita decimal.");
        }
    }

    private static int Specificity(PriceListRule rule) =>
        rule.VariantId.HasValue ? 3 : rule.ProductId.HasValue ? 2 : rule.CategoryId.HasValue ? 1 : 0;
}
