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
    public Guid? SupplierDeliveryNoteLineId { get; private set; }
    public SupplierDeliveryNoteLine? SupplierDeliveryNoteLine { get; private set; }
    public Guid? ReferenceId { get; private set; }

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

    public static StockMovement FromSupplierDeliveryNote(
        SupplierDeliveryNote note, SupplierDeliveryNoteLine line) => new()
    {
        Id = Guid.NewGuid(),
        VariantId = line.ArticleVariantId!.Value,
        WarehouseId = note.WarehouseId,
        Type = StockMovementType.SupplierStockIn,
        Reason = StockMovementReason.SupplierReceipt,
        Quantity = line.Quantity,
        UnitCost = line.UnitPrice,
        MovementDateUtc = note.DocumentDate,
        ReferenceDoc = note.DocumentNumber,
        ReferenceId = note.Id,
        SupplierDeliveryNoteLineId = line.Id
    };

    public static StockMovement FromSupplierReturnDraft(
        SupplierReturnDraft draft, SupplierReturnDraftLine line) => new()
    {
        Id = Guid.NewGuid(),
        VariantId = line.ArticleVariantId,
        WarehouseId = draft.SupplierDeliveryNote.WarehouseId,
        Type = StockMovementType.SupplierStockReturn,
        Reason = StockMovementReason.SupplierReturn,
        Quantity = line.Quantity,
        UnitCost = line.SupplierDeliveryNoteLine.UnitPrice,
        MovementDateUtc = draft.CreatedOn,
        ReferenceDoc = $"RESO-{draft.Id:N}",
        ReferenceId = draft.Id
    };
}
