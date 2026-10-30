using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class OrderShippingChargeTests
{
    [Fact]
    public void ShippingChargeParticipatesInOrderTotalsAndItsVatGroup()
    {
        var calculator = new ItalianAccountingCalculator();
        var order = new Order(Guid.NewGuid(), "ORD-SHIPPING");
        var item = new OrderItem(order.Id, Guid.NewGuid(), 1, 100m, 22m);

        Assert.True(order.AddItem(item, calculator).Success);

        var result = order.SetShippingCharge(5.00m, 10m, null, calculator);

        Assert.True(result.Success, result.Error);
        Assert.Equal(5.00m, order.ShippingCost);
        Assert.Equal(10m, order.ShippingVatRate);
        Assert.Null(order.ShippingNatureCode);
        Assert.Equal(105.00m, order.TaxableTotal);
        Assert.Equal(22.50m, order.VatTotal);
        Assert.Equal(127.50m, order.GrossTotal);
        Assert.Collection(result.Data!.VatSummary.OrderBy(group => group.VatRate),
            group =>
            {
                Assert.Equal(10m, group.VatRate);
                Assert.Equal(5.00m, group.TaxableAmount);
                Assert.Equal(0.50m, group.VatAmount);
            },
            group =>
            {
                Assert.Equal(22m, group.VatRate);
                Assert.Equal(100.00m, group.TaxableAmount);
                Assert.Equal(22.00m, group.VatAmount);
            });
    }

    [Fact]
    public void FailedShippingVatClassificationDoesNotChangeOrderOrLineTotals()
    {
        var calculator = new ItalianAccountingCalculator();
        var order = new Order(Guid.NewGuid(), "ORD-SHIPPING-INVALID");
        var item = new OrderItem(order.Id, Guid.NewGuid(), 1, 100m, 22m);
        Assert.True(order.AddItem(item, calculator).Success);

        var taxableBefore = order.TaxableTotal;
        var vatBefore = order.VatTotal;
        var grossBefore = order.GrossTotal;
        var lineTotalBefore = item.LineNetTotal;

        var result = order.SetShippingCharge(5.00m, 0m, null, calculator);

        Assert.False(result.Success);
        Assert.Equal(0.00m, order.ShippingCost);
        Assert.Null(order.ShippingVatRate);
        Assert.Equal(taxableBefore, order.TaxableTotal);
        Assert.Equal(vatBefore, order.VatTotal);
        Assert.Equal(grossBefore, order.GrossTotal);
        Assert.Equal(lineTotalBefore, item.LineNetTotal);
    }
}
