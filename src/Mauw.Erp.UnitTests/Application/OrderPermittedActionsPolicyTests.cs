using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

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

    private static Order NewOrder()
    {
        var order = new Order(Guid.NewGuid(), "ORD-POLICY");
        order.SetFulfillmentWarehouse(Guid.NewGuid());
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), 2, 10m, 22m));
        return order;
    }
}
