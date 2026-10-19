namespace Mauw.Erp.Domain.Pricing;

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
    int Priority = 0);

public sealed record PriceResolutionInput(
    Guid VariantId, Guid ProductId, Guid? CategoryId,
    decimal Quantity, decimal BaseUnitPrice, decimal BaseCost, DateTimeOffset At);

public sealed record ResolvedPrice(
    Guid? PriceListId, Guid? RuleId, decimal ListUnitPrice,
    decimal DiscountPercent, decimal AgreedUnitNetPrice);

public sealed record PricingSnapshot(
    Guid? PriceListId,
    Guid? RuleId,
    decimal ListUnitPrice,
    decimal? AgreedUnitNetPrice,
    bool IsManualOverride,
    string? OverrideReason,
    bool IsCustomerReturn,
    Guid? SourceLineId);
