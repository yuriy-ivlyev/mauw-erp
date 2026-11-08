using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Pricing;

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

    [Fact]
    public void FromDdtItem_RecordsDeliveryAndCustomerReturnDirections()
    {
        var warehouseId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var documentDate = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var ddt = new DdtDocument(Guid.NewGuid(), "DDT-1", warehouseId) { DdtDate = documentDate };
        var deliveryItem = new DdtItem(ddt.Id, variantId, 3, 10m, 22m);
        var returnItem = new DdtItem(ddt.Id, variantId, 2, 10m, 22m)
        {
            PricingSnapshot = new PricingSnapshot(null, null, 10m, null, false, null, true, null)
        };

        var delivery = StockMovement.FromDdtItem(ddt, deliveryItem, warehouseId);
        var customerReturn = StockMovement.FromDdtItem(ddt, returnItem, warehouseId);

        Assert.Equal(StockMovementType.SalesDelivery, delivery.Type);
        Assert.Equal(StockMovementReason.OrderShipment, delivery.Reason);
        Assert.Equal(-3m, delivery.GetEffectiveQuantityDelta());
        Assert.Equal(StockMovementType.CustomerReturn, customerReturn.Type);
        Assert.Equal(StockMovementReason.CustomerReturn, customerReturn.Reason);
        Assert.Equal(2m, customerReturn.GetEffectiveQuantityDelta());
        Assert.Equal(documentDate, delivery.MovementDateUtc);
        Assert.Equal(ddt.DdtNumber, customerReturn.ReferenceDoc);
    }

    [Fact]
    public void FromShowroomSale_UsesOrderDateAndRecordsSignedCustomerReturn()
    {
        var warehouseId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var orderDate = new DateTime(2026, 11, 5, 13, 0, 0, DateTimeKind.Utc);
        var order = new Order(Guid.NewGuid(), "OF-1") { OrderDate = orderDate };
        var saleItem = new OrderItem(order.Id, variantId, 4, 10m, 22m);
        var returnItem = new OrderItem(order.Id, variantId, 2, 10m, 22m)
        {
            PricingSnapshot = new PricingSnapshot(null, null, 10m, null, false, null, true, null)
        };
        var negativeReturnItem = new OrderItem(order.Id, variantId, -1, 10m, 22m);

        var sale = StockMovement.FromShowroomSale(order, saleItem, warehouseId);
        var customerReturn = StockMovement.FromShowroomSale(order, returnItem, warehouseId);
        var negativeReturn = StockMovement.FromShowroomSale(order, negativeReturnItem, warehouseId);

        Assert.Equal(StockMovementType.SalesDelivery, sale.Type);
        Assert.Equal(StockMovementReason.ShowroomSale, sale.Reason);
        Assert.Equal(-4m, sale.GetEffectiveQuantityDelta());
        Assert.Equal(StockMovementType.CustomerReturn, customerReturn.Type);
        Assert.Equal(StockMovementReason.CustomerReturn, customerReturn.Reason);
        Assert.Equal(2m, customerReturn.GetEffectiveQuantityDelta());
        Assert.Equal(1m, negativeReturn.GetEffectiveQuantityDelta());
        Assert.Equal(orderDate, sale.MovementDateUtc);
        Assert.Equal(order.OrderNumber, customerReturn.ReferenceDoc);
    }

    [Fact]
    public void FromCreditNote_RecordsReturnAndRejectsItemWithoutVariant()
    {
        var warehouseId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var issuedAt = new DateTime(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc);
        var invoice = new Invoice(Guid.NewGuid(), "F-1");
        var creditNote = new CreditNote(invoice, "NC-1", "reso", true, warehouseId, issuedAt);
        var item = new CreditNoteItem(
            creditNote, null, variantId, "Articolo", false, 2m, 10m, 20m, 22m, null, 0m);

        var movement = StockMovement.FromCreditNote(creditNote, item, warehouseId, "user-1");
        var itemWithoutVariant = new CreditNoteItem(
            creditNote, null, null, "Articolo", false, 1m, 10m, 10m, 22m, null, 0m);

        Assert.Equal(variantId, movement.VariantId);
        Assert.Equal(StockMovementType.CustomerReturn, movement.Type);
        Assert.Equal(StockMovementReason.CustomerReturn, movement.Reason);
        Assert.Equal(2m, movement.GetEffectiveQuantityDelta());
        Assert.Equal(issuedAt, movement.MovementDateUtc);
        Assert.Equal("user-1", movement.CreatedByUserId);
        Assert.Throws<Mauw.Erp.Domain.Exceptions.DomainException>(() =>
            StockMovement.FromCreditNote(creditNote, itemWithoutVariant, warehouseId, null));
    }

    [Fact]
    public void FromDdtReversal_CreatesOppositeAdjustmentForTheSameWarehouse()
    {
        var warehouseId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var ddt = new DdtDocument(Guid.NewGuid(), "DDT-2", warehouseId);
        var item = new DdtItem(ddt.Id, variantId, 3, 10m, 22m);
        var movement = StockMovement.FromDdtItem(ddt, item, warehouseId);

        var reversal = StockMovement.FromDdtReversal(ddt, movement);

        Assert.Equal(variantId, reversal.VariantId);
        Assert.Equal(warehouseId, reversal.WarehouseId);
        Assert.Equal(StockMovementType.InventoryAdjustment, reversal.Type);
        Assert.Equal(StockMovementReason.InventoryAdjustment, reversal.Reason);
        Assert.Equal(3m, reversal.GetEffectiveQuantityDelta());
        Assert.Equal(ddt.Id, reversal.ReferenceId);
        Assert.Equal(ddt.DdtNumber, reversal.ReferenceDoc);
    }

    [Fact]
    public void FromDdtReversal_RejectsMovementWithoutPersistedDirection()
    {
        var ddt = new DdtDocument(Guid.NewGuid(), "DDT-3");
        var movement = new StockMovement(
            Guid.NewGuid(), StockMovementType.InventoryAdjustment, 2, 0m);

        Assert.Throws<InvalidOperationException>(() =>
            StockMovement.FromDdtReversal(ddt, movement));
    }

    [Fact]
    public void GetEffectiveQuantityDelta_ReturnsZeroOrNullForUnknownLegacyType()
    {
        var zero = new StockMovement(Guid.NewGuid(), (StockMovementType)int.MaxValue, 0, 0m);
        var unknown = new StockMovement(Guid.NewGuid(), (StockMovementType)int.MaxValue, 2, 0m);

        Assert.Equal(0m, zero.GetEffectiveQuantityDelta());
        Assert.Null(unknown.GetEffectiveQuantityDelta());
    }

    [Fact]
    public void GetEffectiveQuantityDelta_HandlesLegacyAdjustmentsWithoutSignedDelta()
    {
        var negative = new StockMovement(
            Guid.NewGuid(), StockMovementType.InventoryAdjustment, -2, 0m);
        var positive = new StockMovement(
            Guid.NewGuid(), StockMovementType.InventoryAdjustment, 2, 0m);
        SetPrivateProperty(negative, nameof(StockMovement.QuantityDelta), null);
        SetPrivateProperty(positive, nameof(StockMovement.QuantityDelta), null);

        Assert.Equal(-2m, negative.GetEffectiveQuantityDelta());
        Assert.Null(positive.GetEffectiveQuantityDelta());
    }

    [Fact]
    public void FromSupplierDeliveryNote_MapsReceiptAndSourceLine()
    {
        var warehouseId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var documentDate = new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);
        var note = new SupplierDeliveryNote
        {
            WarehouseId = warehouseId,
            DocumentNumber = "DDT-F-1",
            DocumentDate = documentDate
        };
        var line = new SupplierDeliveryNoteLine
        {
            ArticleVariantId = variantId,
            Quantity = 5,
            UnitPrice = 12.34m
        };

        var movement = StockMovement.FromSupplierDeliveryNote(note, line);

        Assert.Equal(variantId, movement.VariantId);
        Assert.Equal(warehouseId, movement.WarehouseId);
        Assert.Equal(StockMovementType.SupplierStockIn, movement.Type);
        Assert.Equal(StockMovementReason.SupplierReceipt, movement.Reason);
        Assert.Equal(5m, movement.GetEffectiveQuantityDelta());
        Assert.Equal(12.34m, movement.UnitCost);
        Assert.Equal(documentDate, movement.MovementDateUtc);
        Assert.Equal(note.DocumentNumber, movement.ReferenceDoc);
        Assert.Equal(note.Id, movement.ReferenceId);
        Assert.Equal(line.Id, movement.SupplierDeliveryNoteLineId);
    }

    [Fact]
    public void FromSupplierReturnDraft_MapsOutflowAndUsesIssuedOrCurrentDate()
    {
        var warehouseId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var issuedAt = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
        var note = new SupplierDeliveryNote { WarehouseId = warehouseId };
        var sourceLine = new SupplierDeliveryNoteLine { UnitPrice = 8.75m };
        var draft = new SupplierReturnDraft { IssuedOn = issuedAt };
        var line = new SupplierReturnDraftLine { ArticleVariantId = variantId, Quantity = 3 };
        SetPrivateProperty(draft, nameof(SupplierReturnDraft.SupplierDeliveryNote), note);
        SetPrivateProperty(line, nameof(SupplierReturnDraftLine.SupplierDeliveryNoteLine), sourceLine);

        var issuedMovement = StockMovement.FromSupplierReturnDraft(draft, line);

        Assert.Equal(variantId, issuedMovement.VariantId);
        Assert.Equal(warehouseId, issuedMovement.WarehouseId);
        Assert.Equal(StockMovementType.SupplierStockReturn, issuedMovement.Type);
        Assert.Equal(StockMovementReason.SupplierReturn, issuedMovement.Reason);
        Assert.Equal(-3m, issuedMovement.GetEffectiveQuantityDelta());
        Assert.Equal(8.75m, issuedMovement.UnitCost);
        Assert.Equal(issuedAt, issuedMovement.MovementDateUtc);
        Assert.Equal($"RESO-{draft.Id:N}", issuedMovement.ReferenceDoc);
        Assert.Equal(draft.Id, issuedMovement.ReferenceId);

        draft.IssuedOn = null;
        var before = DateTime.UtcNow;
        var draftMovement = StockMovement.FromSupplierReturnDraft(draft, line);
        var after = DateTime.UtcNow;
        Assert.InRange(draftMovement.MovementDateUtc, before, after);
    }

    [Fact]
    public void FromOrderAdjustment_RecordsBothDirectionsAndRejectsZero()
    {
        var order = new Order(Guid.NewGuid(), "OF-2");
        var variantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var decrease = StockMovement.FromOrderAdjustment(
            order, variantId, -3, warehouseId, "user-2", "scarico");
        var increase = StockMovement.FromOrderAdjustment(
            order, variantId, 2, warehouseId, "user-2", "rientro");

        Assert.Equal(StockMovementType.SalesDelivery, decrease.Type);
        Assert.Equal(-3m, decrease.GetEffectiveQuantityDelta());
        Assert.Equal(StockMovementType.CustomerReturn, increase.Type);
        Assert.Equal(2m, increase.GetEffectiveQuantityDelta());
        Assert.All([decrease, increase], movement =>
        {
            Assert.Equal(StockMovementReason.OrderAdjustment, movement.Reason);
            Assert.Equal(variantId, movement.VariantId);
            Assert.Equal(warehouseId, movement.WarehouseId);
            Assert.Equal(order.Id, movement.ReferenceId);
            Assert.Equal(order.OrderNumber, movement.ReferenceDoc);
            Assert.Equal("user-2", movement.CreatedByUserId);
        });
        Assert.Throws<Mauw.Erp.Domain.Exceptions.DomainException>(() =>
            StockMovement.FromOrderAdjustment(order, variantId, 0, warehouseId, null, null));
    }

    [Fact]
    public void FromDdtReversal_RejectsMovementWithoutWarehouse()
    {
        var ddt = new DdtDocument(Guid.NewGuid(), "DDT-4");
        var movement = new StockMovement(
            Guid.NewGuid(), StockMovementType.SalesDelivery, 2, 0m);
        SetPrivateProperty(movement, nameof(StockMovement.WarehouseId), null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            StockMovement.FromDdtReversal(ddt, movement));

        Assert.Contains("magazzino", exception.Message);
    }

    private static void SetPrivateProperty<T>(T instance, string propertyName, object? value) =>
        typeof(T).GetProperty(propertyName)!.SetValue(instance, value);

}
