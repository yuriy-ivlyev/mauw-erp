using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class StockMovementTests
{
    [Theory]
    [InlineData(StockMovementType.PurchaseReceipt, 4, 4)]
    [InlineData(StockMovementType.CustomerReturn, 3, 3)]
    [InlineData(StockMovementType.SupplierStockIn, 5, 5)]
    [InlineData(StockMovementType.SalesDelivery, 2, -2)]
    [InlineData(StockMovementType.SupplierStockReturn, 6, -6)]
    public void GetEffectiveQuantityDelta_LegacyDirectedMovement_ReturnsSignedQuantity(
        StockMovementType type,
        int quantity,
        decimal expected)
    {
        var movement = new StockMovement(Guid.NewGuid(), type, quantity, 0m);

        Assert.Equal(expected, movement.GetEffectiveQuantityDelta());
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(-1, -1)]
    public void GetEffectiveQuantityDelta_NewAdjustmentPreservesWholeSignedQuantity(
        int quantity,
        decimal expected)
    {
        var movement = new StockMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StockMovementReason.InventoryAdjustment,
            quantity);

        Assert.Equal(expected, movement.GetEffectiveQuantityDelta());
    }

    public static TheoryData<decimal> FractionalQuantities => new()
    {
        2.5m,
        -1.25m
    };

    [Theory]
    [MemberData(nameof(FractionalQuantities))]
    public void NewAdjustmentRejectsFractionalQuantity(decimal quantity)
    {
        Assert.Throws<Mauw.Erp.Domain.Exceptions.DomainException>(() =>
            new StockMovement(
                Guid.NewGuid(),
                Guid.NewGuid(),
                StockMovementReason.InventoryAdjustment,
                quantity));
    }

}
