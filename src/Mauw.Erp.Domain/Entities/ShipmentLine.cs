namespace Mauw.Erp.Domain.Entities;

public sealed class ShipmentLine : Entity<Guid>
{
    public Guid ShipmentId { get; private set; }
    public Guid OrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int QuantityShipped { get; private set; }

    public Shipment Shipment { get; private set; } = null!;
    public OrderItem OrderLine { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    public ShipmentLine(
        Guid shipmentId,
        Guid orderLineId,
        Guid productId,
        string sku,
        int quantityShipped)
    {
        if (shipmentId == Guid.Empty)
            throw new ArgumentException("A shipment is required.", nameof(shipmentId));
        if (orderLineId == Guid.Empty)
            throw new ArgumentException("An order line is required.", nameof(orderLineId));
        if (productId == Guid.Empty)
            throw new ArgumentException("A product is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("An SKU is required.", nameof(sku));
        if (quantityShipped <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantityShipped), "A shipment line must have a positive quantity.");

        Id = Guid.NewGuid();
        ShipmentId = shipmentId;
        OrderLineId = orderLineId;
        ProductId = productId;
        Sku = sku.Trim();
        QuantityShipped = quantityShipped;
    }

    private ShipmentLine() { }
}
