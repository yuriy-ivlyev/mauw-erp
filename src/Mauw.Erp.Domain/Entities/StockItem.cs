using Mauw.Erp.Domain.Exceptions;

namespace Mauw.Erp.Domain.Entities;

public class StockItem : Entity<Guid>
{
    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public Guid ProductVariantId { get; private set; }
    public ProductVariant ProductVariant { get; private set; } = null!;
    public decimal QuantityOnHand { get; private set; }
    public decimal QuantityReserved { get; private set; }
    public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;
    public decimal? UnitCost { get; private set; }

    protected StockItem() : base()
    {
    }

    public StockItem(
        Guid warehouseId,
        Guid productVariantId,
        decimal quantityOnHand = 0,
        decimal? unitCost = null) : base()
    {
        EnsureWholeQuantity(quantityOnHand);
        WarehouseId = warehouseId;
        ProductVariantId = productVariantId;
        QuantityOnHand = quantityOnHand;
        UnitCost = unitCost;
    }

    public void ApplyMovement(decimal delta)
    {
        EnsureWholeQuantity(delta);
        QuantityOnHand += delta;
        UpdateTimestamp();
    }

    public void Reserve(decimal qty)
    {
        EnsureWholeQuantity(qty);
        QuantityReserved += qty;
        UpdateTimestamp();
    }

    public void ReleaseReservation(decimal qty)
    {
        EnsureWholeQuantity(qty);
        QuantityReserved = Math.Max(0, QuantityReserved - qty);
        UpdateTimestamp();
    }

    public void SetUnitCost(decimal cost)
    {
        UnitCost = cost;
        UpdateTimestamp();
    }

    private static void EnsureWholeQuantity(decimal quantity)
    {
        if (quantity != decimal.Truncate(quantity))
            throw new DomainException("La quantita di merce deve essere un numero intero.");
    }
}
