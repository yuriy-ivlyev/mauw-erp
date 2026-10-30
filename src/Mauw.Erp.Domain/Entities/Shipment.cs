namespace Mauw.Erp.Domain.Entities;

public sealed class Shipment : Entity<Guid>
{
    private readonly List<ShipmentLine> _lines = [];

    public Guid OrderId { get; private set; }
    public string ShipmentNumber { get; private set; } = string.Empty;
    public string DdtNumber { get; private set; } = string.Empty;
    public string? CarrierName { get; private set; }
    public string? TrackingCode { get; private set; }
    public int BoxCount { get; private set; }
    public decimal? TotalWeightKg { get; private set; }
    public DateTime ShippedAtUtc { get; private set; }

    public Order Order { get; private set; } = null!;
    public IReadOnlyCollection<ShipmentLine> Lines => _lines;

    public Shipment(
        Guid orderId,
        string shipmentNumber,
        string ddtNumber,
        string? carrierName,
        string? trackingCode,
        int boxCount,
        decimal? totalWeightKg,
        DateTime shippedAtUtc)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("An order is required.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(shipmentNumber))
            throw new ArgumentException("A shipment number is required.", nameof(shipmentNumber));
        if (string.IsNullOrWhiteSpace(ddtNumber))
            throw new ArgumentException("A DDT number is required.", nameof(ddtNumber));
        if (boxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(boxCount), "At least one box is required.");
        if (totalWeightKg < 0m)
            throw new ArgumentOutOfRangeException(nameof(totalWeightKg), "Weight cannot be negative.");
        if (shippedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Shipment time must be UTC.", nameof(shippedAtUtc));

        Id = Guid.NewGuid();
        OrderId = orderId;
        ShipmentNumber = shipmentNumber.Trim();
        DdtNumber = ddtNumber.Trim();
        CarrierName = string.IsNullOrWhiteSpace(carrierName) ? null : carrierName.Trim();
        TrackingCode = string.IsNullOrWhiteSpace(trackingCode) ? null : trackingCode.Trim();
        BoxCount = boxCount;
        TotalWeightKg = totalWeightKg;
        ShippedAtUtc = shippedAtUtc;
    }

    private Shipment() { }

    public void AddLine(ShipmentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.ShipmentId != Id)
            throw new InvalidOperationException("The shipment line belongs to another shipment.");
        if (_lines.Any(existing => existing.OrderLineId == line.OrderLineId))
            throw new InvalidOperationException("A shipment can contain each order line only once.");

        _lines.Add(line);
    }
}
