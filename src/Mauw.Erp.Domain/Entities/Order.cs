namespace Mauw.Erp.Domain.Entities;

using Enums;

/// <summary>
/// Order entity representing a sales order.
/// </summary>
public class Order : Entity
{
    public Guid ContactId { get; set; }
    public string OrderNumber { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal CustomerDiscountRate { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrossTotal { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = [];

    public Order(
        Guid contactId,
        string orderNumber,
        decimal customerDiscountRate = 0
    ) : base()
    {
        ContactId = contactId;
        OrderNumber = orderNumber;
        Status = OrderStatus.Draft;
        OrderDate = DateTime.UtcNow;
        CustomerDiscountRate = customerDiscountRate;
        TaxableTotal = 0;
        VatTotal = 0;
        GrossTotal = 0;
    }

    private Order() { } // For EF Core

    /// <summary>
    /// Adds an item to the order and updates totals.
    /// </summary>
    public void AddItem(OrderItem item)
    {
        Items.Add(item);
        UpdateTotals();
    }

    /// <summary>
    /// Recalculates order totals from items.
    /// </summary>
    public void UpdateTotals()
    {
        TaxableTotal = Items.Sum(i => i.LineNetTotal);
        VatTotal = Items.Sum(i => i.LineVatTotal);
        GrossTotal = TaxableTotal + VatTotal;
        UpdateTimestamp();
    }

    /// <summary>
    /// Marks order as confirmed.
    /// </summary>
    public void Confirm()
    {
        if (Items.Count == 0)
            throw new InvalidOperationException("Cannot confirm an order without items.");

        Status = OrderStatus.Confirmed;
        UpdateTimestamp();
    }
}

/// <summary>
/// OrderItem entity representing a line item in an order.
/// </summary>
public class OrderItem : Entity
{
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal LineNetTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineVatTotal { get; set; }
    public decimal LineGrossTotal { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;

    public OrderItem(
        Guid orderId,
        Guid variantId,
        int quantity,
        decimal unitNetPrice,
        decimal vatRate
    ) : base()
    {
        OrderId = orderId;
        VariantId = variantId;
        Quantity = quantity;
        UnitNetPrice = unitNetPrice;
        VatRate = vatRate;

        // Calculate line totals
        LineNetTotal = decimal.Round(unitNetPrice * quantity, 2, MidpointRounding.AwayFromZero);
        LineVatTotal = decimal.Round(LineNetTotal * (vatRate / 100), 2, MidpointRounding.AwayFromZero);
        LineGrossTotal = LineNetTotal + LineVatTotal;
    }

    private OrderItem() { } // For EF Core
}
