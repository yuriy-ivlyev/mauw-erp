namespace Mauw.Erp.Domain.Entities;

using Mauw.Erp.Domain.Enums;

/// <summary>
/// StockMovement entity tracking inventory movements.
/// </summary>
public class StockMovement : Entity<Guid>
{
    public Guid VariantId { get; set; }
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceDoc { get; set; }
    public Guid? WarehouseId { get; private set; }
    public StockMovementReason? Reason { get; private set; }
    public DateTime MovementDateUtc { get; private set; } = DateTime.UtcNow;
    public string? Notes { get; private set; }
    public string? CreatedByUserId { get; private set; }

    // Navigation
    public ProductVariant Variant { get; set; } = null!;
    public Warehouse? Warehouse { get; private set; }

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

    public StockMovement(
        Guid variantId,
        Guid warehouseId,
        StockMovementReason reason,
        decimal quantity,
        string? notes = null,
        string? createdByUserId = null,
        string? documentReference = null) : base()
    {
        VariantId = variantId;
        WarehouseId = warehouseId;
        Reason = reason;
        Type = StockMovementType.InventoryAdjustment;
        Quantity = (int)Math.Round(Math.Abs(quantity));
        UnitCost = 0;
        ReferenceDoc = documentReference ?? "";
        MovementDateUtc = DateTime.UtcNow;
        Notes = notes;
        CreatedByUserId = createdByUserId;
    }

    private StockMovement() { } // For EF Core
}
