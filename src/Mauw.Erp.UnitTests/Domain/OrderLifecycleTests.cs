using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class OrderLifecycleTests
{
    [Fact]
    public void RemovingDraftItemRecalculatesOrderTotalsAndAllowsAnEmptyDraft()
    {
        var order = new Order(Guid.NewGuid(), "ORD-REMOVE");
        var item = new OrderItem(order.Id, Guid.NewGuid(), 2, 10m, 22m);
        var calculator = new ItalianAccountingCalculator();
        Assert.True(order.AddItem(item, calculator).Success);

        var result = order.RemoveItem(item.Id, calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(order.Items);
        Assert.Equal(0.00m, order.TaxableTotal);
        Assert.Equal(0.00m, order.VatTotal);
        Assert.Equal(0.00m, order.GrossTotal);
    }

    [Fact]
    public void CashDeskSaleCompletesDirectlyAndRecordsPayment()
    {
        var order = NewOrder(OrderFulfillmentType.CashDesk);
        order.GrossTotal = 125m;

        order.FinalizeCashSale("Carta", "txn-123");

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal("Carta", order.PaymentMethod);
        Assert.Equal("txn-123", order.PaymentTransactionDetails);
        Assert.Throws<InvalidOperationException>(() => order.StartPicking(Guid.NewGuid()));
    }

    [Fact]
    public void CashSaleRequiresDraftCashDeskOrderWithLinesAndPaymentMethod()
    {
        var emptyOrder = new Order(Guid.NewGuid(), "ORD-EMPTY", fulfillmentType: OrderFulfillmentType.CashDesk);
        Assert.Throws<InvalidOperationException>(() => emptyOrder.FinalizeCashSale("Contanti", null));

        var shippingOrder = NewOrder(OrderFulfillmentType.StandardShipping);
        Assert.Throws<InvalidOperationException>(() => shippingOrder.FinalizeCashSale("Contanti", null));

        var suspendedOrder = NewOrder(OrderFulfillmentType.CashDesk);
        suspendedOrder.Suspend("POS-1");
        Assert.Throws<InvalidOperationException>(() => suspendedOrder.FinalizeCashSale("Contanti", null));

        var noPaymentMethod = NewOrder(OrderFulfillmentType.CashDesk);
        Assert.Throws<ArgumentException>(() => noPaymentMethod.FinalizeCashSale(" ", null));
        Assert.Equal(OrderStatus.Draft, noPaymentMethod.Status);
    }

    [Fact]
    public void ShippingOrderFollowsPickingPackingShippingAndCompletionTransitions()
    {
        var order = NewOrder(OrderFulfillmentType.StandardShipping);
        var warehouseId = Guid.NewGuid();
        order.SetFulfillmentWarehouse(warehouseId);

        order.Confirm();
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(warehouseId, order.FulfillmentWarehouseId);

        order.StartPicking(Guid.NewGuid());
        order.CompletePacking(2, 4.75m);
        order.Ship("Corriere", "TRACK-123", "DDT-123");

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal(2, order.BoxCount);
        Assert.Equal(4.75m, order.WeightKg);
        Assert.Equal("DDT-123", order.DdtNumber);

        order.MarkDelivered();
        order.Complete();
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.Cancel("Too late"));
    }

    [Fact]
    public void CancellationIsLimitedToUnshippedStates()
    {
        var order = NewOrder(OrderFulfillmentType.StandardShipping);
        order.SetFulfillmentWarehouse(Guid.NewGuid());
        order.Confirm();
        order.Cancel("Cliente ha rinunciato");

        Assert.Equal(OrderStatus.Cancelled, order.Status);

        var onHold = NewOrder(OrderFulfillmentType.StandardShipping);
        onHold.PlaceOnHold("Verifica");
        onHold.Cancel("Annullamento");
        Assert.Equal(OrderStatus.Cancelled, onHold.Status);
    }

    [Fact]
    public void ShippingCanBeSplitAndTheRemainingQuantityCanBeClosedOrResumed()
    {
        var partial = NewOrder(OrderFulfillmentType.StandardShipping);
        partial.SetFulfillmentWarehouse(Guid.NewGuid());
        partial.Confirm();
        partial.StartPicking(Guid.NewGuid());
        partial.CompletePacking(1, 2m);

        partial.Ship("Corriere", "TRACK-1", "DDT-1", allRemainingItemsShipped: false);
        Assert.Equal(OrderStatus.PartiallyShipped, partial.Status);

        partial.PlaceOnHold("Attesa disponibilità");
        Assert.Throws<InvalidOperationException>(() => partial.Cancel("Annulla"));
        partial.ReleaseHold();
        Assert.Equal(OrderStatus.PartiallyShipped, partial.Status);

        partial.Ship("Corriere", "TRACK-2", "DDT-2");
        Assert.Equal(OrderStatus.Shipped, partial.Status);

        var closeRemainder = NewOrder(OrderFulfillmentType.StandardShipping);
        closeRemainder.SetFulfillmentWarehouse(Guid.NewGuid());
        closeRemainder.Confirm();
        closeRemainder.StartPicking(Guid.NewGuid());
        closeRemainder.CompletePacking(1, 2m);
        closeRemainder.Ship("Corriere", "TRACK-3", "DDT-3", allRemainingItemsShipped: false);
        closeRemainder.CancelRemainingLines("Saldo non disponibile");
        Assert.Equal(OrderStatus.Completed, closeRemainder.Status);
    }

    [Fact]
    public void ExistingCassaStatusValuesRemainDatabaseCompatible()
    {
        Assert.Equal(0, (int)OrderStatus.Draft);
        Assert.Equal(1, (int)OrderStatus.Confirmed);
        Assert.Equal(2, (int)OrderStatus.Suspended);
        Assert.Equal(3, (int)OrderStatus.Terminated);
    }

    private static Order NewOrder(OrderFulfillmentType fulfillmentType)
    {
        var order = new Order(Guid.NewGuid(), "ORD-LIFECYCLE", fulfillmentType: fulfillmentType);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), 2, 10m, 22m));
        return order;
    }
}
