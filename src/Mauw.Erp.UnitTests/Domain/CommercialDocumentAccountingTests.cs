using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.UnitTests.Domain;

public class CommercialDocumentAccountingTests
{
    [Theory]
    [InlineData("Order")]
    [InlineData("Invoice")]
    [InlineData("DDT")]
    public void AggregatesCalculateVatOncePerGroup(string type)
    {
        var fixture = CreateFixture(type);

        Assert.True(fixture.Add(0.03m, 22m, null, 0m).Success);
        var result = fixture.Add(0.03m, 22m, null, 0m);

        Assert.True(result.Success, result.Error);
        Assert.Equal((0.06m, 0.01m, 0.07m), fixture.Totals());
        Assert.Equal(result.Data!.TotalGross, fixture.Totals().Gross);
        Assert.All(fixture.Items(), item =>
        {
            Assert.Equal(0.03m, item.LineNetTotal);
            Assert.Null(item.LineVatTotal);
            Assert.Null(item.LineGrossTotal);
        });
    }

    [Theory]
    [InlineData("Order")]
    [InlineData("Invoice")]
    [InlineData("DDT")]
    public void InvalidAdditionLeavesExistingLinesAndTotalsUnchanged(string type)
    {
        var fixture = CreateFixture(type);
        Assert.True(fixture.Add(10m, 22m, null, 0m).Success);
        var totals = fixture.Totals();
        var original = Assert.Single(fixture.Items());

        var result = fixture.Add(100m, 0m, null, 0m);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Same(original, Assert.Single(fixture.Items()));
        Assert.Equal(10m, original.LineNetTotal);
        Assert.Equal(totals, fixture.Totals());
    }

    [Theory]
    [InlineData("Order")]
    [InlineData("Invoice")]
    [InlineData("DDT")]
    public void ZeroRateNatureGroupsRemainDistinct(string type)
    {
        var fixture = CreateFixture(type);
        Assert.True(fixture.Add(10m, 0m, "N3.2", 0m).Success);

        var result = fixture.Add(20m, 0m, "N4", 0m);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Data!.VatSummary.Length);
        Assert.Equal((30m, 0m, 30m), fixture.Totals());
        Assert.Equal(["N3.2", "N4"], fixture.Items().Select(item => item.NatureCode));
    }

    [Theory]
    [InlineData("Order")]
    [InlineData("Invoice")]
    [InlineData("DDT")]
    public void DiscountsDoNotRoundTheDiscountedUnitPrice(string type)
    {
        var fixture = CreateFixture(type);

        var result = fixture.Add(0.03m, 22m, null, 50m);

        Assert.True(result.Success, result.Error);
        var line = Assert.Single(fixture.Items());
        Assert.Equal(0.03m, line.UnitNetPrice);
        Assert.Equal(50m, line.DiscountPercent);
        Assert.Equal(0.02m, line.LineNetTotal);
        Assert.Equal((0.02m, 0m, 0.02m), fixture.Totals());
    }

    [Fact]
    public void InvoiceAccountingPreservesFractionalFiscalQuantity()
    {
        var calculator = new ItalianAccountingCalculator();
        var invoice = new Invoice(Guid.NewGuid(), "INV-FRACTIONAL");

        var result = invoice.AddItem(new(invoice.Id, Guid.NewGuid(), 0.125m, 10.12345678m, 22m), calculator);

        Assert.True(result.Success, result.Error);
        Assert.Equal(0.125m, Assert.Single(invoice.Items).Quantity);
        Assert.Equal(1.27m, invoice.TaxableTotal);
        Assert.Equal(0.28m, invoice.VatTotal);
        Assert.Equal(1.55m, invoice.GrossTotal);
    }

    [Fact]
    public void PersistencePreservesUnitPriceDiscountAndNaturePrecision()
    {
        var options = new DbContextOptionsBuilder<MauwDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only")
            .Options;
        using var context = new MauwDbContext(options);

        foreach (var type in new[] { typeof(OrderItem), typeof(InvoiceItem), typeof(DdtItem) })
        {
            var entity = context.Model.FindEntityType(type)!;
            Assert.Equal(8, entity.FindProperty("UnitNetPrice")!.GetScale());
            Assert.Equal("numeric", entity.FindProperty("DiscountPercent")!.GetColumnType());
            Assert.Equal(4, entity.FindProperty("NatureCode")!.GetMaxLength());
            Assert.Equal(2, entity.FindProperty("LineNetTotal")!.GetScale());
            if (type == typeof(InvoiceItem))
                Assert.Equal(8, entity.FindProperty("Quantity")!.GetScale());
            Assert.True(entity.FindProperty("LineVatTotal")!.IsNullable);
            Assert.True(entity.FindProperty("LineGrossTotal")!.IsNullable);
        }
    }

    private static Fixture CreateFixture(string type)
    {
        var calculator = new ItalianAccountingCalculator();
        switch (type)
        {
            case "Order":
                var order = new Order(Guid.NewGuid(), "TEST");
                return new(
                    (price, rate, nature, discount) => order.AddItem(
                        new(order.Id, Guid.NewGuid(), 1, price, rate, natureCode: nature, discountPercent: discount), calculator),
                    () => order.Items,
                    () => (order.TaxableTotal, order.VatTotal, order.GrossTotal));
            case "Invoice":
                var invoice = new Invoice(Guid.NewGuid(), "TEST");
                return new(
                    (price, rate, nature, discount) => invoice.AddItem(
                        new(invoice.Id, Guid.NewGuid(), 1, price, rate, nature, discount), calculator),
                    () => invoice.Items,
                    () => (invoice.TaxableTotal, invoice.VatTotal, invoice.GrossTotal));
            case "DDT":
                var ddt = new DdtDocument(Guid.NewGuid(), "TEST");
                return new(
                    (price, rate, nature, discount) => ddt.AddItem(
                        new(ddt.Id, Guid.NewGuid(), 1, price, rate, nature, discount), calculator),
                    () => ddt.Items,
                    () => (ddt.TaxableTotal, ddt.VatTotal, ddt.GrossTotal));
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    [Fact]
    public void NewOrderGraphReceivesMatchingGeneratedKeysBeforeSaving()
    {
        var calculator = new ItalianAccountingCalculator();
        var order = new Order(Guid.NewGuid(), "ORD-GRAPH");
        Assert.True(order.AddItem(new(order.Id, Guid.NewGuid(), 5, 18.85245902m, 22m), calculator).Success);
        var options = new DbContextOptionsBuilder<MauwDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only").Options;
        using var context = new MauwDbContext(options);

        context.Orders.Add(order);

        Assert.NotEqual(Guid.Empty, order.Id);
        var item = Assert.Single(order.Items);
        Assert.Equal(order.Id, item.OrderId);
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(115m, order.GrossTotal);
    }

    private sealed record Fixture(
        Func<decimal, decimal, string?, decimal, AccountingResult<CalculatedDocument>> Add,
        Func<IEnumerable<ICommercialDocumentLine>> Items,
        Func<(decimal Net, decimal Vat, decimal Gross)> Totals);
}
