using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public class OrderAdjustmentTests
{
    private static readonly ItalianAccountingCalculator Calculator = new();

    [Fact]
    public void OnlyACompletedOrderCanBeReopenedAndOnlyOnce()
    {
        var draft = new Order(Guid.NewGuid(), "ORD-1", fulfillmentType: OrderFulfillmentType.CashDesk);
        Assert.False(draft.BeginAdjustment("tester").Success);
        Assert.False(draft.IsUnderAdjustment);

        var order = CompletedCashOrder((Guid.NewGuid(), 2, 10m, 22m, null));
        var reopened = order.BeginAdjustment("  Mario Rossi ");

        Assert.True(reopened.Success);
        Assert.True(order.IsUnderAdjustment);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal("Mario Rossi", order.AdjustmentStartedBy);
        Assert.NotNull(order.AdjustmentStartedAt);
        Assert.False(order.BeginAdjustment("altro").Success);

        Assert.True(order.CompleteAdjustment().Success);
        Assert.False(order.IsUnderAdjustment);
        Assert.Null(order.AdjustmentStartedAt);
        Assert.Null(order.AdjustmentStartedBy);
        Assert.False(order.CompleteAdjustment().Success);
    }

    [Fact]
    public void LinesAreLockedUntilTheOrderIsReopened()
    {
        var variant = Guid.NewGuid();
        var order = CompletedCashOrder((variant, 2, 10m, 22m, null));
        var item = order.Items.Single();

        var result = order.AdjustLines([new(item.Id, 3)], [], Calculator);

        Assert.False(result.Success);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(24.40m, order.GrossTotal);
    }

    [Fact]
    public void ChangingQuantitiesRecalculatesTotalsFromTheSharedCalculator()
    {
        var order = CompletedCashOrder((Guid.NewGuid(), 2, 10m, 22m, null), (Guid.NewGuid(), 1, 100m, 4m, null));
        Assert.True(order.BeginAdjustment("tester").Success);
        var first = order.Items.First();
        var second = order.Items.Last();

        var result = order.AdjustLines([new(first.Id, 5), new(second.Id, 2)], [], Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Equal(5, first.Quantity);
        Assert.Equal(50m, first.LineNetTotal);
        Assert.Equal(200m, second.LineNetTotal);
        Assert.Equal(250m, order.TaxableTotal);
        Assert.Equal(11m + 8m, order.VatTotal);
        Assert.Equal(269m, order.GrossTotal);
        Assert.Equal(2, result.Data!.VatSummary.Length);
    }

    [Fact]
    public void LinesCanBeRemovedAndAddedAtTheSameTime()
    {
        var removedVariant = Guid.NewGuid();
        var addedVariant = Guid.NewGuid();
        var order = CompletedCashOrder((removedVariant, 2, 10m, 22m, null), (Guid.NewGuid(), 1, 10m, 22m, null));
        Assert.True(order.BeginAdjustment("tester").Success);
        var kept = order.Items.Last();
        var added = new OrderItem(order.Id, addedVariant, 3, 20m, 10m);

        var result = order.AdjustLines([new(kept.Id, 1)], [added], Calculator);

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain(order.Items, item => item.VariantId == removedVariant);
        Assert.Contains(order.Items, item => item.VariantId == addedVariant && item.LineNetTotal == 60m);
        Assert.Equal(70m, order.TaxableTotal);
        Assert.Equal(2.2m + 6m, order.VatTotal);
        Assert.Equal(78.2m, order.GrossTotal);
    }

    [Fact]
    public void InvalidAdjustmentLeavesTheOrderUntouched()
    {
        var order = CompletedCashOrder((Guid.NewGuid(), 2, 10m, 22m, null), (Guid.NewGuid(), 1, 10m, 22m, null));
        Assert.True(order.BeginAdjustment("tester").Success);
        var first = order.Items.First();
        var second = order.Items.Last();
        var gross = order.GrossTotal;

        var unknown = order.AdjustLines([new(first.Id, 3), new(Guid.NewGuid(), 1)], [], Calculator);
        var zero = order.AdjustLines([new(first.Id, 0), new(second.Id, 1)], [], Calculator);
        var duplicate = order.AdjustLines([new(first.Id, 3), new(first.Id, 4)], [], Calculator);
        var empty = order.AdjustLines([], [], Calculator);
        var foreign = order.AdjustLines([new(first.Id, 1)], [new OrderItem(Guid.NewGuid(), Guid.NewGuid(), 1, 1m, 22m)], Calculator);
        var invalidFiscal = order.AdjustLines(
            [new(first.Id, 1)], [new OrderItem(order.Id, Guid.NewGuid(), 1, 10m, 0m)], Calculator);

        Assert.All([unknown, zero, duplicate, empty, foreign, invalidFiscal], result => Assert.False(result.Success));
        Assert.Equal(2, first.Quantity);
        Assert.Equal(1, second.Quantity);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(gross, order.GrossTotal);
    }

    [Fact]
    public void ReturnLinesAreKeptOrRemovedButNeverRequantified()
    {
        var order = CompletedCashOrder((Guid.NewGuid(), 2, 10m, 22m, null));
        var returnLine = Persisted(new OrderItem(order.Id, Guid.NewGuid(), -1, 10m, 22m)
        {
            PricingSnapshot = new PricingSnapshot(null, null, 10m, 10m, false, null, true, null)
        });
        order.Items.Add(returnLine);
        Assert.True(order.BeginAdjustment("tester").Success);
        var sale = order.Items.First();

        var changed = order.AdjustLines([new(sale.Id, 2), new(returnLine.Id, -2)], [], Calculator);
        var unchanged = order.AdjustLines([new(sale.Id, 3), new(returnLine.Id, -1)], [], Calculator);

        Assert.False(changed.Success);
        Assert.True(unchanged.Success, unchanged.Error);
        Assert.Equal(3, sale.Quantity);
        Assert.Equal(-1, returnLine.Quantity);
    }

    [Fact]
    public void NewLinesMustBePositiveSalesThatMoveStock()
    {
        var order = CompletedCashOrder((Guid.NewGuid(), 2, 10m, 22m, null));
        Assert.True(order.BeginAdjustment("tester").Success);
        var sale = order.Items.Single();
        var billingOnly = new OrderItem(order.Id, Guid.NewGuid(), 1, 10m, 22m);
        billingOnly.MarkBillingOnly();

        Assert.False(order.AdjustLines([new(sale.Id, 2)], [new OrderItem(order.Id, Guid.NewGuid(), 0, 10m, 22m)], Calculator).Success);
        Assert.False(order.AdjustLines([new(sale.Id, 2)], [new OrderItem(order.Id, Guid.NewGuid(), -1, 10m, 22m)], Calculator).Success);
        Assert.False(order.AdjustLines([new(sale.Id, 2)], [billingOnly], Calculator).Success);
        Assert.Single(order.Items);
    }

    [Fact]
    public void ShippingOrdersKeepFullyShippedQuantitiesAlignedWithTheAdjustedQuantity()
    {
        var order = new Order(Guid.NewGuid(), "ORD-SHIP");
        var item = Persisted(new OrderItem(order.Id, Guid.NewGuid(), 4, 10m, 22m));
        Assert.True(order.AddItem(item, Calculator).Success);
        item.RecordShipment(4);
        TransitionToCompleted(order);
        Assert.True(order.BeginAdjustment("tester").Success);

        Assert.True(order.AdjustLines([new(item.Id, 2)], [], Calculator).Success);
        Assert.Equal(2, item.QuantityShipped);
        Assert.Equal(0, item.QuantityRemaining);

        Assert.True(order.AdjustLines([new(item.Id, 6)], [], Calculator).Success);
        Assert.Equal(6, item.QuantityShipped);
        Assert.Equal(0, item.QuantityRemaining);
    }

    [Fact]
    public void LegacyCompletedOrderEditsAreBlockedWhileUnderAdjustment()
    {
        var order = CompletedCashOrder((Guid.NewGuid(), 2, 10m, 22m, null));
        Assert.True(order.BeginAdjustment("tester").Success);
        var item = order.Items.Single();

        Assert.False(order.AddCompletedBillingItem(new OrderItem(order.Id, Guid.NewGuid(), 1, 5m, 22m), Calculator).Success);
        Assert.False(order.ExcludeCompletedBillingItem(item.Id, Calculator).Success);
        Assert.False(order.SetShippingCharge(5m, 22m, null, Calculator).Success);
        Assert.Single(order.Items);
        Assert.False(item.IsExcludedFromInvoice);
        Assert.Equal(0m, order.ShippingCost);
    }

    [Fact]
    public void StockDeltaIsAFurtherDischargeWhenQuantityGrowsAndAReturnWhenItShrinks()
    {
        var grown = Guid.NewGuid();
        var shrunk = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var added = Guid.NewGuid();
        var unchanged = Guid.NewGuid();
        var before = new Dictionary<Guid, int> { [grown] = -3, [shrunk] = -5, [removed] = -2, [unchanged] = -1 };
        var after = new Dictionary<Guid, int> { [grown] = -7, [shrunk] = -2, [added] = -4, [unchanged] = -1 };

        var deltas = OrderAdjustmentStockCalculator.CalculateDeltas(before, after)
            .ToDictionary(delta => delta.VariantId, delta => delta.DeltaOnHand);

        Assert.Equal(4, deltas.Count);
        Assert.Equal(-4, deltas[grown]);
        Assert.Equal(3, deltas[shrunk]);
        Assert.Equal(2, deltas[removed]);
        Assert.Equal(-4, deltas[added]);
        Assert.False(deltas.ContainsKey(unchanged));
    }

    [Fact]
    public void StockEffectsCountOnlyStockMovingLinesAndTreatReturnsAsInbound()
    {
        var variant = Guid.NewGuid();
        var order = CompletedCashOrder((variant, 4, 10m, 22m, null), (variant, 1, 10m, 22m, null));
        var billingOnly = new OrderItem(order.Id, variant, 9, 10m, 22m);
        billingOnly.MarkBillingOnly();
        var returned = new OrderItem(order.Id, variant, -2, 10m, 22m);
        order.Items.Add(billingOnly);
        order.Items.Add(returned);

        var effects = OrderAdjustmentStockCalculator.GetStockEffects(order.Items);

        Assert.Equal(-4 - 1 + 2, effects[variant]);
    }

    [Fact]
    public void AdjustmentMovementIsLedgerConsistentInBothDirections()
    {
        var order = CompletedCashOrder((Guid.NewGuid(), 1, 10m, 22m, null));
        var variant = Guid.NewGuid();
        var warehouse = Guid.NewGuid();

        var discharge = StockMovement.FromOrderAdjustment(order, variant, -3, warehouse, "user-1", "nota");
        var inbound = StockMovement.FromOrderAdjustment(order, variant, 2, warehouse, "user-1", "nota");

        Assert.Equal(StockMovementType.SalesDelivery, discharge.Type);
        Assert.Equal(-3m, discharge.QuantityDelta);
        Assert.Equal(3, discharge.Quantity);
        Assert.Equal(StockMovementType.CustomerReturn, inbound.Type);
        Assert.Equal(2m, inbound.QuantityDelta);
        Assert.All([discharge, inbound], movement =>
        {
            Assert.Equal(StockMovementReason.OrderAdjustment, movement.Reason);
            Assert.Equal(order.Id, movement.ReferenceId);
            Assert.Equal(order.OrderNumber, movement.ReferenceDoc);
            Assert.Equal(warehouse, movement.WarehouseId);
            Assert.Equal("user-1", movement.CreatedByUserId);
        });
        Assert.Throws<Mauw.Erp.Domain.Exceptions.DomainException>(() =>
            StockMovement.FromOrderAdjustment(order, variant, 0, warehouse, null, null));
    }

    [Fact]
    public void InvoiceKeepsIdentityAndNumberWhileSynchronizingMultiRateTotals()
    {
        var order = CompletedCashOrder((Guid.NewGuid(), 2, 10m, 22m, null), (Guid.NewGuid(), 1, 100m, 4m, null));
        var invoice = new Invoice(order.ContactId, "FAT-202610-00007") { OrderId = order.Id };
        Assert.True(invoice.SyncFromOrder(order, Calculator).Success);
        Assert.True(invoice.Confirm(Calculator).Success);
        var invoiceId = invoice.Id;
        var number = invoice.InvoiceNumber;
        var confirmedAt = invoice.ConfirmedAt;

        Assert.True(order.BeginAdjustment("tester").Success);
        Assert.True(order.AdjustLines([new(order.Items.First().Id, 5), new(order.Items.Last().Id, 2)], [], Calculator).Success);
        var synchronized = invoice.SyncFromOrder(order, Calculator);

        Assert.True(synchronized.Success, synchronized.Error);
        Assert.Equal(invoiceId, invoice.Id);
        Assert.Equal(number, invoice.InvoiceNumber);
        Assert.Equal(confirmedAt, invoice.ConfirmedAt);
        Assert.Equal(2, invoice.Items.Count);
        Assert.Equal(250m, invoice.TaxableTotal);
        Assert.Equal(19m, invoice.VatTotal);
        Assert.Equal(269m, invoice.GrossTotal);
        Assert.Collection(synchronized.Data!.VatSummary.OrderBy(group => group.VatRate),
            group => Assert.Equal((4m, 200m, 8m), (group.VatRate, group.TaxableAmount, group.VatAmount)),
            group => Assert.Equal((22m, 50m, 11m), (group.VatRate, group.TaxableAmount, group.VatAmount)));
        Assert.Equal((order.TaxableTotal, order.VatTotal, order.GrossTotal),
            (invoice.TaxableTotal, invoice.VatTotal, invoice.GrossTotal));
    }

    [Fact]
    public void InvoiceSdiLifecycleLocksTheDocumentProgressively()
    {
        var order = CompletedCashOrder((Guid.NewGuid(), 1, 10m, 22m, null));
        var invoice = new Invoice(order.ContactId, "FAT-1") { OrderId = order.Id };
        Assert.True(invoice.SyncFromOrder(order, Calculator).Success);

        Assert.Equal(SdiStatus.NotTransmitted, invoice.SdiStatus);
        Assert.False(invoice.MarkSdiPending().Success);
        Assert.True(invoice.Confirm(Calculator).Success);
        Assert.True(invoice.CanBeAdjustedBeforeSdi);
        Assert.False(invoice.MarkSdiAccepted().Success);

        Assert.True(invoice.MarkSdiPending().Success);
        Assert.Equal(SdiStatus.Pending, invoice.SdiStatus);
        Assert.False(invoice.CanBeAdjustedBeforeSdi);
        Assert.False(invoice.IsSdiSubmitted);
        Assert.False(invoice.MarkSdiPending().Success);

        Assert.True(invoice.MarkSdiTransmitted(DateTime.UtcNow).Success);
        Assert.Equal(SdiStatus.Transmitted, invoice.SdiStatus);
        Assert.True(invoice.IsSdiSubmitted);
        Assert.False(invoice.MarkSdiTransmitted(DateTime.UtcNow).Success);
        Assert.False(invoice.SyncFromOrder(order, Calculator).Success);

        Assert.True(invoice.MarkSdiAccepted().Success);
        Assert.Equal(SdiStatus.Accepted, invoice.SdiStatus);
        Assert.True(invoice.IsSdiSubmitted);
        Assert.False(invoice.CanBeAdjustedBeforeSdi);
    }

    [Fact]
    public void LegacyInvoiceWithOnlyASubmissionTimestampIsReportedAsTransmitted()
    {
        var invoice = new Invoice(Guid.NewGuid(), "FAT-OLD");
        typeof(Invoice).GetProperty(nameof(Invoice.SdiSubmittedAt))!
            .SetValue(invoice, DateTime.UtcNow);

        Assert.Equal(SdiStatus.Transmitted, invoice.SdiStatus);
        Assert.True(invoice.IsSdiSubmitted);
        Assert.False(invoice.CanBeAdjustedBeforeSdi);
    }

    private static Order CompletedCashOrder(
        params (Guid VariantId, int Quantity, decimal UnitNetPrice, decimal VatRate, string? Nature)[] lines)
    {
        var order = new Order(Guid.NewGuid(), "ORD-ADJ", fulfillmentType: OrderFulfillmentType.CashDesk);
        foreach (var line in lines)
            Assert.True(order.AddItem(
                Persisted(new OrderItem(order.Id, line.VariantId, line.Quantity, line.UnitNetPrice, line.VatRate,
                    natureCode: line.Nature)), Calculator).Success);
        order.FinalizeCashSale("Contanti", null);
        return order;
    }

    // Identifiers are generated by EF Core on insert; domain tests need distinct ones for loaded lines.
    private static OrderItem Persisted(OrderItem item)
    {
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(item, Guid.NewGuid());
        return item;
    }

    private static void TransitionToCompleted(Order order)
    {
        order.SetFulfillmentWarehouse(Guid.NewGuid());
        order.Confirm();
        order.StartPicking(Guid.NewGuid());
        order.CompletePacking(1, 1m);
        order.Ship("Corriere", "TRK", "DDT-1");
        order.Complete();
    }
}
