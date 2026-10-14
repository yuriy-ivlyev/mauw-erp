using FluentValidation;
using Mauw.Erp.Api;
using Mauw.Erp.Contracts;
using Mauw.Erp.Domain;

namespace Mauw.Erp.UnitTests;

public class PricingEngineTests
{
    private readonly PricingEngine _engine = new();

    [Fact]
    public void Calculate_WithZeroDiscount_KeepsListPriceAsFinalNetPrice()
    {
        var result = _engine.Calculate(100m, 40m, 0m, 22m);

        Assert.Equal(140m, result.ListPrice);
        Assert.Equal(140m, result.FinalNetPrice);
        Assert.Equal(30.80m, result.VatAmount);
        Assert.Equal(170.80m, result.FinalGrossPrice);
        Assert.Equal(40m, result.MarginAmount);
        Assert.Equal(28.57m, result.MarginPercent);
    }

    [Fact]
    public void Calculate_WithDefaultMarkup40PercentAndPurchaseCost20_ProducesExpectedListPrice()
    {
        var result = _engine.Calculate(20m, 40m, 0m, 22m);

        Assert.Equal(28m, result.ListPrice);
        Assert.Equal(28m, result.FinalNetPrice);
        Assert.Equal(6.16m, result.VatAmount);
        Assert.Equal(34.16m, result.FinalGrossPrice);
        Assert.Equal(8m, result.MarginAmount);
        Assert.Equal(28.57m, result.MarginPercent);
    }

    [Theory]
    [InlineData(22, 15.40, 85.40)]
    [InlineData(0, 0, 70)]
    public void Calculate_HandlesOrdinaryAndExemptVat(int vatRate, decimal expectedVatAmount, decimal expectedGrossPrice)
    {
        var result = _engine.Calculate(50m, 40m, 0m, vatRate);

        Assert.Equal(70m, result.FinalNetPrice);
        Assert.Equal(expectedVatAmount, result.VatAmount);
        Assert.Equal(expectedGrossPrice, result.FinalGrossPrice);
    }

    [Fact]
    public void Calculate_RoundsDecimalFractionsAwayFromZero()
    {
        var result = _engine.Calculate(12.345m, 17.555m, 7.555m, 22.225m);

        Assert.Equal(14.51m, result.ListPrice);
        Assert.Equal(13.41m, result.FinalNetPrice);
        Assert.Equal(2.98m, result.VatAmount);
        Assert.Equal(16.39m, result.FinalGrossPrice);
        Assert.Equal(1.065m, result.MarginAmount);
        Assert.Equal(7.94m, result.MarginPercent);
    }
}

public class CatalogValidationTests
{
    private readonly IValidator<CreateProductRequest> _validator = new CreateProductRequestValidator();

    [Fact]
    public void CreateProductRequest_ShouldRejectEmptySupplierCode()
    {
        var request = new CreateProductRequest
        {
            SupplierId = Guid.NewGuid(),
            SupplierCode = "",
            InternalCode = "INT-001",
            Name = "Widget",
            DefaultMarkupRate = 30m,
            VatRate = 22m,
            Variants =
            [
                new CreateProductVariantRequest
                {
                    Size = "M",
                    Color = "Blue",
                    Barcode = "123456789",
                    PurchaseCost = 10m,
                    StockQuantity = 10
                }
            ]
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProductRequest.SupplierCode));
    }

    [Fact]
    public void CreateProductRequest_ShouldAcceptValidProductWithVariants()
    {
        var request = new CreateProductRequest
        {
            SupplierId = Guid.NewGuid(),
            SupplierCode = "SUP-001",
            InternalCode = "INT-001",
            Name = "Widget",
            DefaultMarkupRate = 30m,
            VatRate = 22m,
            Variants =
            [
                new CreateProductVariantRequest
                {
                    Size = "M",
                    Color = "Blue",
                    Barcode = "123456789",
                    PurchaseCost = 10m,
                    StockQuantity = 10
                }
            ]
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
