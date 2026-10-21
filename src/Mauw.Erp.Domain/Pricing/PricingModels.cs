namespace Mauw.Erp.Domain.Pricing;

using System.Text.Json.Serialization;

public enum PriceCalculationType
{
    FixedPrice,
    PercentageDiscount,
    CostMarkup
}

public sealed record PriceRuleDefinition(
    Guid? Id,
    PriceCalculationType CalculationType,
    decimal Value,
    Guid? VariantId = null,
    Guid? ProductId = null,
    Guid? CategoryId = null,
    decimal MinimumQuantity = 1m,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null,
    int Priority = 0,
    string? Description = null);

public sealed record PriceResolutionInput(
    Guid VariantId, Guid ProductId, Guid? CategoryId,
    decimal Quantity, decimal BaseUnitPrice, decimal BaseCost, DateTimeOffset At);

public sealed record ResolvedPrice(
    Guid? PriceListId, Guid? RuleId, decimal ListUnitPrice,
    decimal DiscountPercent, decimal AgreedUnitNetPrice, string? DiscountReason = null);

/// <summary>Origine di uno sconto applicato a una riga.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DiscountSource>))]
public enum DiscountSource
{
    PriceList,
    Cashier
}

/// <summary>
/// Uno degli sconti di una riga, con la propria causale. Esattamente uno tra percentuale e importo:
/// l'importo e uno sconto sul totale di riga, espresso in euro con due decimali.
/// </summary>
public sealed record AppliedDiscount(DiscountSource Source, string Reason, decimal? Percent, decimal? Amount);

public sealed record PricingSnapshot(
    Guid? PriceListId,
    Guid? RuleId,
    decimal ListUnitPrice,
    decimal? AgreedUnitNetPrice,
    bool IsManualOverride,
    string? OverrideReason,
    bool IsCustomerReturn,
    Guid? SourceLineId,
    IReadOnlyList<AppliedDiscount>? Discounts = null)
{
    // Il record confronta le liste per riferimento: gli sconti si confrontano per valore e in ordine.
    public bool Equals(PricingSnapshot? other) =>
        other is not null && PriceListId == other.PriceListId && RuleId == other.RuleId &&
        ListUnitPrice == other.ListUnitPrice && AgreedUnitNetPrice == other.AgreedUnitNetPrice &&
        IsManualOverride == other.IsManualOverride && OverrideReason == other.OverrideReason &&
        IsCustomerReturn == other.IsCustomerReturn && SourceLineId == other.SourceLineId &&
        (ReferenceEquals(Discounts, other.Discounts) ||
         (Discounts is not null && other.Discounts is not null && Discounts.SequenceEqual(other.Discounts)));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PriceListId);
        hash.Add(RuleId);
        hash.Add(ListUnitPrice);
        hash.Add(AgreedUnitNetPrice);
        hash.Add(IsManualOverride);
        hash.Add(OverrideReason);
        hash.Add(IsCustomerReturn);
        hash.Add(SourceLineId);
        foreach (var discount in Discounts ?? [])
            hash.Add(discount);
        return hash.ToHashCode();
    }
}
