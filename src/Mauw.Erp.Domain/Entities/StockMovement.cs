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
    /// <summary>Signed ledger amount; null on legacy adjustments whose direction was not persisted.</summary>
    public decimal? QuantityDelta { get; private set; }
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
        QuantityDelta = type == StockMovementType.InventoryAdjustment
            ? quantity
            : GetLegacyDirection(type, quantity);
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
        QuantityDelta = quantity;
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
        QuantityDelta = line.Quantity,
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
        QuantityDelta = -line.Quantity,
        UnitCost = line.SupplierDeliveryNoteLine.UnitPrice,
        MovementDateUtc = draft.IssuedOn ?? DateTime.UtcNow,
        ReferenceDoc = $"RESO-{draft.Id:N}",
        ReferenceId = draft.Id
    };

    public static StockMovement FromDdtItem(DdtDocument ddt, DdtItem item, Guid warehouseId)
    {
        var isCustomerReturn = item.PricingSnapshot?.IsCustomerReturn == true || item.Quantity < 0;
        var quantity = checked((int)Math.Abs((long)item.Quantity));
        var delta = isCustomerReturn ? quantity : -quantity;

        return new StockMovement
        {
            Id = Guid.NewGuid(),
            VariantId = item.VariantId,
            WarehouseId = warehouseId,
            Type = isCustomerReturn ? StockMovementType.CustomerReturn : StockMovementType.SalesDelivery,
            Reason = isCustomerReturn ? StockMovementReason.CustomerReturn : StockMovementReason.OrderShipment,
            Quantity = quantity,
            QuantityDelta = delta,
            UnitCost = 0m,
            MovementDateUtc = ddt.DdtDate,
            ReferenceDoc = ddt.DdtNumber,
            ReferenceId = ddt.Id
        };
    }

    public static StockMovement FromCassaItem(Order order, OrderItem item, Guid warehouseId)
    {
        var isCustomerReturn = item.PricingSnapshot?.IsCustomerReturn == true || item.Quantity < 0;
        var quantity = checked((int)Math.Abs((long)item.Quantity));
        var delta = isCustomerReturn ? quantity : -quantity;

        return new StockMovement
        {
            Id = Guid.NewGuid(),
            VariantId = item.VariantId,
            WarehouseId = warehouseId,
            Type = isCustomerReturn ? StockMovementType.CustomerReturn : StockMovementType.SalesDelivery,
            Reason = isCustomerReturn ? StockMovementReason.CustomerReturn : StockMovementReason.ShowroomSale,
            Quantity = quantity,
            QuantityDelta = delta,
            UnitCost = 0m,
            MovementDateUtc = order.OrderDate,
            ReferenceDoc = order.OrderNumber,
            ReferenceId = order.Id
        };
    }

    public static StockMovement FromDdtReversal(DdtDocument ddt, StockMovement movement)
    {
        var delta = movement.GetEffectiveQuantityDelta()
            ?? throw new InvalidOperationException("Il movimento DDT non ha una quantita stornabile.");
        var reversal = new StockMovement(
            movement.VariantId,
            movement.WarehouseId
                ?? throw new InvalidOperationException("Il movimento DDT non ha un magazzino."),
            StockMovementReason.InventoryAdjustment,
            -delta,
            $"Storno cancellazione DDT {ddt.DdtNumber}",
            documentReference: ddt.DdtNumber)
        {
            ReferenceId = ddt.Id
        };
        return reversal;
    }

    public decimal? GetEffectiveQuantityDelta()
    {
        if (QuantityDelta.HasValue)
            return QuantityDelta.Value;

        if (Quantity == 0)
            return 0m;

        return GetLegacyDirection(Type, Quantity);
    }

    private static decimal? GetLegacyDirection(StockMovementType type, int quantity) => type switch
    {
        StockMovementType.PurchaseReceipt or
        StockMovementType.CustomerReturn or
        StockMovementType.SupplierStockIn => Math.Abs((decimal)quantity),
        StockMovementType.SalesDelivery or
        StockMovementType.SupplierStockReturn => -Math.Abs((decimal)quantity),
        StockMovementType.InventoryAdjustment when quantity < 0 => quantity,
        StockMovementType.InventoryAdjustment => null,
        _ => null
    };
}
