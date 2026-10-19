using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public class CatalogPriceCalculatorTests
{
    public static TheoryData<decimal, decimal, decimal> Markups => new()
    {
        { 10m, 30m, 13m }, { 10m, 200m, 30m }, { 10m, 9999m, 1009.9m },
        { 0m, 30m, 0m }, { 0.00000005m, 10m, 0.00000006m }
    };

    [Theory]
    [MemberData(nameof(Markups))]
    public void MarkupIsOnlyAnExplicitDeterministicEntryAid(decimal cost, decimal markup, decimal expected)
    {
        var calculator = new CatalogPriceCalculator();
        var result = calculator.FromMarkup(cost, markup);
        Assert.True(result.Success, result.Error);
        Assert.Equal(expected, result.Data!.Value);
        Assert.Equal(result, calculator.FromMarkup(cost, markup));
    }

    [Fact]
    public void ReadingPriceNeverUsesCostProductOrLegacyMarkupAndDoesNotInherit()
    {
        var product = new Product(Guid.NewGuid(), "S", "P", "Test", 9999m, 22m, 10m) { SellingPrice = 50m };
        var variant = new ProductVariant(product.Id, "SKU", 10m)
            { Product = product, SellingPrice = 17.12345678m, MarkupRate = 50m };
        var calculator = new CatalogPriceCalculator();
        Assert.Equal(17.12345678m, calculator.GetStoredPrice(variant).Data!.Value);
        variant.PurchaseCost = 999m;
        product.SellingPrice = 500m;
        variant.MarkupRate = 9999m;
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

    public static TheoryData<decimal, decimal> InvalidMarkups => new()
        { { -1m, 20m }, { 10m, -1m }, { 10m, 10000m }, { 10m, 20.001m }, { decimal.MaxValue, 9999m } };

    [Theory]
    [MemberData(nameof(InvalidMarkups))]
    public void InvalidMarkupAndOverflowReturnErrors(decimal cost, decimal markup) =>
        Assert.False(new CatalogPriceCalculator().FromMarkup(cost, markup).Success);
}
