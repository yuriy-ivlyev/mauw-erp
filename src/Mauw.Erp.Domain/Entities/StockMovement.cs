namespace Mauw.Erp.Domain.Entities;

using Enums;

/// <summary>
/// StockMovement entity tracking inventory movements.
/// </summary>
public class StockMovement : Entity
{
    public Guid VariantId { get; set; }
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceDoc { get; set; }

    // Navigation
    public ProductVariant Variant { get; set; } = null!;

    public StockMovement(
        Guid variantId,
        StockMovementType type,
        int quantity,
        decimal unitCost,
        string? referenceDoc = null
    ) : base()
    {
        VariantId = variantId;
        Type = type;
        Quantity = quantity;
        UnitCost = unitCost;
        ReferenceDoc = referenceDoc;
    }

    private StockMovement() { } // For EF Core
}
