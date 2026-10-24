using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class SupplierPurchaseOrderTests
{
    [Fact]
    public void ConfirmChangesDraftToConfirmedAndRotatesRevision()
    {
        var order = new SupplierPurchaseOrder();
        order.Lines.Add(new SupplierPurchaseOrderLine { QuantityOrdered = 3, UnitCost = 12.345678m });
        var revision = order.Version;

        order.Confirm();

        Assert.Equal(SupplierPurchaseOrderStatus.Confirmed, order.Status);
        Assert.NotEqual(revision, order.Version);
    }

    [Theory]
    [InlineData(0, SupplierPurchaseOrderStatus.Confirmed)]
    [InlineData(3, SupplierPurchaseOrderStatus.Completed)]
    [InlineData(4, SupplierPurchaseOrderStatus.PartiallyReceived)]
    public void ReceiptProgressReflectsLineQuantities(int received, SupplierPurchaseOrderStatus expected)
    {
        var order = new SupplierPurchaseOrder
        {
            Status = SupplierPurchaseOrderStatus.Confirmed,
            Lines = [new SupplierPurchaseOrderLine { QuantityOrdered = 3, QuantityReceived = received }]
        };

        order.RefreshProgress();

        Assert.Equal(expected, order.Status);
    }

    [Fact]
    public void ClosedWithDiscrepanciesIsNotOverwrittenByLaterProgressRefresh()
    {
        var order = new SupplierPurchaseOrder
        {
            Status = SupplierPurchaseOrderStatus.ClosedWithDiscrepancies,
            Lines = [new SupplierPurchaseOrderLine { QuantityOrdered = 3, QuantityReceived = 1 }]
        };

        order.RefreshProgress();

        Assert.Equal(SupplierPurchaseOrderStatus.ClosedWithDiscrepancies, order.Status);
    }
}
