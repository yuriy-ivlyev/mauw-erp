namespace Mauw.Erp.Domain.Entities;

using Enums;

/// <summary>
/// Order entity representing a sales order.
/// </summary>
public class Order : Entity<Guid>
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
    )
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