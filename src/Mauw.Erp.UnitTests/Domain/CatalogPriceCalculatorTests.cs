using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public class CatalogPriceCalculatorTests
{
    [Fact]
    public void ReadingPriceNeverUsesCostOrProductPriceAndDoesNotInherit()
    {
        var product = new Product(Guid.NewGuid(), "S", "P", "Test", 22m, 10m) { SellingPrice = 50m };
        var variant = new ProductVariant(product.Id, "SKU", 10m, product.SupplierId)
            { Product = product, SellingPrice = 17.12345678m };
        var calculator = new CatalogPriceCalculator();
        Assert.Equal(17.12345678m, calculator.GetStoredPrice(variant).Data!.Value);
        variant.PurchaseCost = 999m;
        product.SellingPrice = 500m;
        Assert.Equal(17.12345678m, calculator.GetStoredPrice(variant).Data!.Value);
        variant.SellingPrice = null;
        Assert.False(calculator.GetStoredPrice(variant).Success);
        variant.SellingPrice = 0m;
        Assert.Equal(0m, calculator.GetStoredPrice(variant).Data!.Value);
    }

    public static TheoryData<decimal?> InvalidPrices => new()
        { null, -1m, 1.123456789m, decimal.MaxValue };

    [Theory]
    [MemberData(nameof(InvalidPrices))]
    public void InvalidOrMissingPricesReturnExplicitFailure(decimal? price) =>
        Assert.False(new CatalogPriceCalculator().Validate(price).Success);

    public static TheoryData<decimal> ValidPrices => new()
        { 0m, 0.00000001m, 17.12345678m, 99999999999999.99999999m };

    [Theory]
    [MemberData(nameof(ValidPrices))]
    public void ValidPricesKeepFullPrecision(decimal price)
    {
        var result = new CatalogPriceCalculator().Validate(price);
        Assert.True(result.Success, result.Error);
        Assert.Equal(price, result.Data!.Value);
    }
}
