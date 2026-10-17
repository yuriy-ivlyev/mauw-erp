namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// OrderItem entity representing a line item in an order.
/// </summary>
public class OrderItem : Entity<Guid>
{
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal LineNetTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineVatTotal { get; set; }
    public decimal LineGrossTotal { get; set; }
    public int MaxQuantity { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;

    public OrderItem(
        Guid orderId,
        Guid variantId,
        int quantity,
        decimal unitNetPrice,
        decimal vatRate,
        int maxQuantity = 0
    ) : base()
    {
        OrderId = orderId;
        VariantId = variantId;
        Quantity = quantity;
        UnitNetPrice = unitNetPrice;
        VatRate = vatRate;
        MaxQuantity = maxQuantity > 0 ? maxQuantity : quantity;

        // Calculate line totals
        LineNetTotal = decimal.Round(unitNetPrice * quantity, 2, MidpointRounding.AwayFromZero);
        LineVatTotal = decimal.Round(LineNetTotal * (vatRate / 100), 2, MidpointRounding.AwayFromZero);
        LineGrossTotal = LineNetTotal + LineVatTotal;
    }

    private OrderItem() { } // For EF Core
}