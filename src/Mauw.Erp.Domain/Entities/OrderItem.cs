namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// OrderItem entity representing a line item in an order.
/// </summary>
public class OrderItem : Entity<Guid>, ICommercialDocumentLine
{
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public int QuantityShipped { get; private set; }
    public bool IsBillingOnly { get; private set; }
    public bool IsExcludedFromInvoice { get; private set; }
    public int QuantityRemaining => IsBillingOnly || IsExcludedFromInvoice
        ? 0
        : Math.Max(0, Quantity - QuantityShipped);
    public decimal UnitNetPrice { get; set; }
    public decimal LineNetTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal? LineVatTotal { get; set; }
    public decimal? LineGrossTotal { get; set; }
    public string? NatureCode { get; set; }
    public decimal DiscountPercent { get; set; }
    public int MaxQuantity { get; set; }
    decimal ICommercialDocumentLine.Quantity => Quantity;
    public Mauw.Erp.Domain.Pricing.PricingSnapshot? PricingSnapshot { get; init; }

    // Navigation
    public Order Order { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;

    public OrderItem(
        Guid orderId,
        Guid variantId,
        int quantity,
        decimal unitNetPrice,
        decimal vatRate,
        int maxQuantity = 0,
        string? natureCode = null,
        decimal discountPercent = 0m
    ) : base()
    {
        OrderId = orderId;
        VariantId = variantId;
        Quantity = quantity;
        UnitNetPrice = unitNetPrice;
        VatRate = vatRate;
        MaxQuantity = maxQuantity > 0 ? maxQuantity : quantity;
        NatureCode = natureCode;
        DiscountPercent = discountPercent;
    }

    private OrderItem() { } // For EF Core

    public void SetShippedQuantityFromHistory(int quantityShipped)
    {
        if (quantityShipped < QuantityShipped || quantityShipped > Math.Max(0, Quantity))
            throw new ArgumentOutOfRangeException(nameof(quantityShipped));

        QuantityShipped = quantityShipped;
        UpdateTimestamp();
    }

    public void RecordShipment(int quantity)
    {
        if (quantity < 0 || quantity > QuantityRemaining)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Shipment quantity exceeds the order line remainder.");

        QuantityShipped = checked(QuantityShipped + quantity);
        UpdateTimestamp();
    }

    public void MarkBillingOnly()
    {
        IsBillingOnly = true;
        UpdateTimestamp();
    }

    /// <summary>Changes the quantity of a line of an order reopened for a pre-SDI adjustment.</summary>
    internal void AdjustQuantity(int quantity)
    {
        Quantity = quantity;
        UpdateTimestamp();
    }

    /// <summary>Keeps a fully shipped line consistent with its adjusted quantity (no residual to ship).</summary>
    internal void AlignShippedQuantityWithQuantity()
    {
        QuantityShipped = Math.Max(0, Quantity);
        UpdateTimestamp();
    }

    /// <summary>True for lines that are posted to inventory (not billing-only and not excluded from the invoice).</summary>
    public bool AffectsStock => !IsBillingOnly && !IsExcludedFromInvoice;

    public bool IsCustomerReturnLine => PricingSnapshot?.IsCustomerReturn == true || Quantity < 0;

    public void ExcludeFromInvoice()
    {
        IsExcludedFromInvoice = true;
        UpdateTimestamp();
    }
}