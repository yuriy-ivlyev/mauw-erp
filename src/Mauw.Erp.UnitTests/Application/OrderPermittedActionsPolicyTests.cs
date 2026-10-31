using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Application;

public sealed class OrderPermittedActionsPolicyTests
{
    [Fact]
    public void DraftActionsDependOnFulfillmentAndExposeDialogRequirements()
    {
        var shipping = NewOrder();
        var cash = new Order(Guid.NewGuid(), "ORD-CASH", fulfillmentType: OrderFulfillmentType.CashDesk);

        Assert.Collection(OrderPermittedActionsPolicy.GetFor(shipping),
            confirm =>
            {
                Assert.Equal("Confirm", confirm.Action);
                Assert.Equal("/api/v1/orders/" + shipping.Id + "/confirm", confirm.Endpoint);
                Assert.False(confirm.RequiresDialog);
            },
            cancel =>
            {
                Assert.Equal("Cancel", cancel.Action);
                Assert.Equal("CancelReason", cancel.DialogType);
                Assert.True(cancel.RequiresDialog);
            });

        Assert.Equal("FinalizeCashSale", OrderPermittedActionsPolicy.GetFor(cash)[0].Action);
        Assert.Equal("PaymentDetails", OrderPermittedActionsPolicy.GetFor(cash)[0].DialogType);
    }

    [Fact]
    public void ApprovalAndWarehouseStatusesReturnOnlyTheirPermittedActions()
    {
        var pending = NewOrder();
        pending.RequireApproval("Verifica fido");
        Assert.Equal(["Confirm", "Cancel"],
            OrderPermittedActionsPolicy.GetFor(pending).Select(action => action.Action));

        var confirmed = NewOrder();
        confirmed.Confirm();
        Assert.Equal(["StartPicking", "PutOnHold", "Cancel"],
            OrderPermittedActionsPolicy.GetFor(confirmed).Select(action => action.Action));

        confirmed.StartPicking(Guid.NewGuid());
        Assert.Equal(["Ship", "PutOnHold", "Cancel"],
            OrderPermittedActionsPolicy.GetFor(confirmed).Select(action => action.Action));

        confirmed.CompletePacking(1, 2m);
        Assert.Equal(["Ship", "PutOnHold", "Cancel"],
            OrderPermittedActionsPolicy.GetFor(confirmed).Select(action => action.Action));
    }

    [Fact]
    public void PartialShipmentHoldAndTerminalStatusesHaveCorrectActions()
    {
        var partial = NewOrder();
        partial.Confirm();
        partial.StartPicking(Guid.NewGuid());
        partial.CompletePacking(1, 2m);
        partial.Ship("Corriere", "TRACK", "DDT", allRemainingItemsShipped: false);
        Assert.Equal(["Ship", "CancelRemainingLines", "PutOnHold"],
            OrderPermittedActionsPolicy.GetFor(partial).Select(action => action.Action));

        partial.PlaceOnHold("Attesa");
        Assert.Equal(["Resume"], OrderPermittedActionsPolicy.GetFor(partial).Select(action => action.Action));

        partial.ReleaseHold();
        partial.Ship("Corriere", "TRACK2", "DDT2");
        Assert.Equal(["Complete"], OrderPermittedActionsPolicy.GetFor(partial).Select(action => action.Action));
        partial.Complete();
        Assert.Empty(OrderPermittedActionsPolicy.GetFor(partial));

        var cancelled = NewOrder();
        cancelled.Cancel("Motivo");
        Assert.Empty(OrderPermittedActionsPolicy.GetFor(cancelled));
    }

    [Fact]
    public void MappingIncludesStatusDescriptionReadOnlyFlagAndActions()
    {
        var draft = NewOrder();
        var draftDto = OrderMapper.MapToDto(draft);
        Assert.Equal("Bozza", draftDto.StatusDescription);
        Assert.False(draftDto.IsReadOnly);
        Assert.NotEmpty(draftDto.PermittedActions);

        draft.Confirm();
        var confirmedDto = OrderMapper.MapToDto(draft);
        Assert.Equal("Confermato", confirmedDto.StatusDescription);
        Assert.True(confirmedDto.IsReadOnly);
    }

    [Fact]
    public void CompletedOrderWithUntransmittedInvoiceOffersOnlyTheReopenAction()
    {
        var (order, invoice) = CompletedOrderWithInvoice();

        var action = Assert.Single(OrderPermittedActionsPolicy.GetFor(order, invoice));

        Assert.Equal("ReopenForAdjustment", action.Action);
        Assert.Equal("Modifica Righe (Pre-SDI)", action.Label);
        Assert.Equal("warning", action.Style);
        Assert.False(action.RequiresDialog);
        Assert.Equal("POST", action.Method);
        Assert.Equal($"/api/v1/orders/{order.Id}/reopen-adjustment", action.Endpoint);
        Assert.True(OrderPermittedActionsPolicy.IsReadOnly(order, invoice));
    }

    [Fact]
    public void OrderUnderAdjustmentUnlocksLinesAndOffersTheSyncActionOnly()
    {
        var (order, invoice) = CompletedOrderWithInvoice();
        Assert.True(order.BeginAdjustment("tester").Success);

        var action = Assert.Single(OrderPermittedActionsPolicy.GetFor(order, invoice));

        Assert.Equal("SyncAndFinalizeInvoice", action.Action);
        Assert.Equal("Aggiorna Fattura e Chiudi", action.Label);
        Assert.Equal("primary", action.Style);
        Assert.False(action.RequiresDialog);
        Assert.Equal($"/api/v1/orders/{order.Id}/sync-invoice", action.Endpoint);
        Assert.False(OrderPermittedActionsPolicy.IsReadOnly(order, invoice));
        var dto = OrderMapper.MapToDto(order, invoice: invoice);
        Assert.True(dto.IsUnderAdjustment);
        Assert.False(dto.IsReadOnly);
        Assert.Equal("NotTransmitted", dto.InvoiceSdiStatus);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Transmitted")]
    [InlineData("Accepted")]
    public void InvoiceAlreadyQueuedOrTransmittedLocksTheOrderEvenIfFlaggedUnderAdjustment(string state)
    {
        var (order, invoice) = CompletedOrderWithInvoice();
        Assert.True(order.BeginAdjustment("tester").Success);
        MoveInvoiceTo(invoice, state);

        Assert.Empty(OrderPermittedActionsPolicy.GetFor(order, invoice));
        Assert.True(OrderPermittedActionsPolicy.IsReadOnly(order, invoice));
        Assert.True(OrderMapper.MapToDto(order, invoice: invoice).IsReadOnly);

        var reopened = CompletedOrderWithInvoice();
        MoveInvoiceTo(reopened.Invoice, state);
        Assert.Empty(OrderPermittedActionsPolicy.GetFor(reopened.Order, reopened.Invoice));
        Assert.True(OrderPermittedActionsPolicy.IsReadOnly(reopened.Order, reopened.Invoice));
    }

    [Fact]
    public void CompletedOrderWithoutInvoiceOffersNoAdjustmentAction()
    {
        var (order, _) = CompletedOrderWithInvoice();

        Assert.Empty(OrderPermittedActionsPolicy.GetFor(order));
        Assert.True(OrderPermittedActionsPolicy.IsReadOnly(order));
    }

    private static (Order Order, Invoice Invoice) CompletedOrderWithInvoice()
    {
        var calculator = new ItalianAccountingCalculator();
        var order = new Order(Guid.NewGuid(), "ORD-CASH", fulfillmentType: OrderFulfillmentType.CashDesk);
        Assert.True(order.AddItem(new OrderItem(order.Id, Guid.NewGuid(), 2, 10m, 22m), calculator).Success);
        order.FinalizeCashSale("Contanti", null);
        var invoice = new Invoice(order.ContactId, "FAT-202610-00001") { OrderId = order.Id };
        Assert.True(invoice.SyncFromOrder(order, calculator).Success);
        Assert.True(invoice.Confirm(calculator).Success);
        return (order, invoice);
    }

    private static void MoveInvoiceTo(Invoice invoice, string state)
    {
        Assert.True(invoice.MarkSdiPending().Success);
        if (state == "Pending")
            return;

        Assert.True(invoice.MarkSdiTransmitted(DateTime.UtcNow).Success);
        if (state == "Accepted")
            Assert.True(invoice.MarkSdiAccepted().Success);
    }

    private static Order NewOrder()
    {
        var order = new Order(Guid.NewGuid(), "ORD-POLICY");
        order.SetFulfillmentWarehouse(Guid.NewGuid());
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), 2, 10m, 22m));
        return order;
    }
}
