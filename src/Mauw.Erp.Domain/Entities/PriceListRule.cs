using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Domain.Entities;

public sealed class PriceListRule : Entity<Guid>
{
    public Guid PriceListId { get; private set; }
    public PriceCalculationType CalculationType { get; private set; }
    public decimal Value { get; private set; }
    public Guid? VariantId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public decimal MinimumQuantity { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public int Priority { get; private set; }
    public string? Description { get; private set; }

    private PriceListRule() { }

    internal PriceListRule(Guid priceListId, PriceRuleDefinition definition)
        : base(definition.Id ?? Guid.NewGuid())
    {
        PriceListId = priceListId;
        Apply(definition);
    }

    internal void Apply(PriceRuleDefinition definition)
    {
        CalculationType = definition.CalculationType;
        Value = definition.Value;
        VariantId = definition.VariantId;
        ProductId = definition.ProductId;
        CategoryId = definition.CategoryId;
        MinimumQuantity = definition.MinimumQuantity;
        ValidFrom = definition.ValidFrom?.ToUniversalTime();
        ValidTo = definition.ValidTo?.ToUniversalTime();
        Priority = definition.Priority;
        Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim();
    }
}
