namespace Mauw.Erp.Domain;

public class StockMovement
{
    public Guid Id { get; private set; }
    public Guid VariantId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public string? ReferenceDoc { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public StockMovement(
        Guid variantId,
        StockMovementType type,
        int quantity,
        decimal unitCost,
        string? referenceDoc,
        DateTimeOffset? createdAt = null)
    {
        Id = Guid.NewGuid();
        VariantId = variantId;
        Type = type;
        Quantity = quantity;
        UnitCost = unitCost;
        ReferenceDoc = referenceDoc;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }
}
