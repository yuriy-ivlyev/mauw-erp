using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

/// <summary>
/// Unit tests for the Italian Fiscal Pricing Engine.
/// Validates commercial rounding (MidpointRounding.AwayFromZero) compliance with DPR 633/72.
/// </summary>
public class PricingEngineTests
{
    private readonly PricingEngine _pricingEngine = new();

    #region Commercial Rounding Tests

    [Theory]
    [InlineData(12.455, 12.46)] // Should round up (away from zero)
    [InlineData(12.454, 12.45)] // Should round down (towards zero)  
    [InlineData(12.445, 12.45)] // Should round down at .5
    [InlineData(12.465, 12.47)] // Should round up at .5
    public void CalculatePrice_CommercialRounding_CorrectNetPriceRounding(decimal testValue, decimal expected)
    {
        // Arrange
        // Use testValue directly as the net price (0% markup, 0% discount, 0% VAT)
        var purchaseCost = testValue;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, 0, 0, 0);

        // Assert - The final net price should follow commercial rounding
        Assert.Equal(expected, result.FinalNetPrice, 2);
    }

    [Fact]
    public void CalculatePrice_CommercialRounding_12455Rounds12_46()
    {
        // Arrange - Create a calculation that results in 12.455
        const decimal purchaseCost = 10.0m;
        const decimal markupRate = 24.55m; // Results in 12.455
        const decimal discountRate = 0m;
        const decimal vatRate = 0m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(12.46m, result.FinalNetPrice); // 12.455 rounds to 12.46 with AwayFromZero
    }

    [Fact]
    public void CalculatePrice_CommercialRounding_12454Rounds12_45()
    {
        // Arrange - Create a calculation that results in 12.454
        const decimal purchaseCost = 10.0m;
        const decimal markupRate = 24.54m; // Results in 12.454
        const decimal discountRate = 0m;
        const decimal vatRate = 0m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(12.45m, result.FinalNetPrice); // 12.454 rounds to 12.45 with AwayFromZero
    }

    #endregion

    #region Basic Pricing Tests

    [Fact]
    public void CalculatePrice_NoDiscountNoMarkup_CalculatesCorrectly()
    {
        // Arrange
        const decimal purchaseCost = 20.00m;
        const decimal markupRate = 0m;
        const decimal discountRate = 0m;
        const decimal vatRate = 22m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(20.00m, result.ListPrice);
        Assert.Equal(20.00m, result.FinalNetPrice);
        Assert.Equal(4.40m, result.VatAmount);
        Assert.Equal(24.40m, result.FinalGrossPrice);
        Assert.Equal(0m, result.MarginAmount);
        Assert.Equal(0m, result.MarginPercent);
    }

    #endregion

    #region Markup Tests

    [Fact]
    public void CalculatePrice_40PercentMarkup_CorrectListPrice()
    {
        // Arrange
        const decimal purchaseCost = 20.00m;
        const decimal markupRate = 40m;
        const decimal discountRate = 0m;
        const decimal vatRate = 22m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(28.00m, result.ListPrice); // 20 * 1.40
        Assert.Equal(28.00m, result.FinalNetPrice);
        Assert.Equal(6.16m, result.VatAmount); // 28 * 0.22
        Assert.Equal(34.16m, result.FinalGrossPrice);
        Assert.Equal(8.00m, result.MarginAmount); // 28 - 20
        Assert.Equal(28.57m, result.MarginPercent); // (8 / 28) * 100
    }

    #endregion

    #region Discount Tests

    [Fact]
    public void CalculatePrice_10PercentDiscount_CorrectFinalPrice()
    {
        // Arrange
        const decimal purchaseCost = 20.00m;
        const decimal markupRate = 40m;
        const decimal discountRate = 10m;
        const decimal vatRate = 22m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(28.00m, result.ListPrice);
        Assert.Equal(25.20m, result.FinalNetPrice); // 28 * 0.90
        Assert.Equal(5.54m, result.VatAmount); // 25.20 * 0.22
        Assert.Equal(30.74m, result.FinalGrossPrice);
        Assert.Equal(5.20m, result.MarginAmount); // 25.20 - 20
        Assert.Equal(20.63m, result.MarginPercent); // (5.20 / 25.20) * 100
    }

    #endregion

    #region VAT Rate Tests

    [Fact]
    public void CalculatePrice_StandardVat22Percent_CorrectVatAmount()
    {
        // Arrange
        const decimal purchaseCost = 20.00m;
        const decimal markupRate = 0m;
        const decimal discountRate = 0m;
        const decimal vatRate = 22m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(4.40m, result.VatAmount);
        Assert.Equal(24.40m, result.FinalGrossPrice);
    }

    [Fact]
    public void CalculatePrice_ReducedVat10Percent_CorrectVatAmount()
    {
        // Arrange
        const decimal purchaseCost = 20.00m;
        const decimal markupRate = 0m;
        const decimal discountRate = 0m;
        const decimal vatRate = 10m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(2.00m, result.VatAmount);
        Assert.Equal(22.00m, result.FinalGrossPrice);
    }

    [Fact]
    public void CalculatePrice_ExemptVat0Percent_NoVat()
    {
        // Arrange
        const decimal purchaseCost = 20.00m;
        const decimal markupRate = 0m;
        const decimal discountRate = 0m;
        const decimal vatRate = 0m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(20.00m, result.FinalGrossPrice);
    }

    #endregion

    #region Margin Calculation Tests

    [Fact]
    public void CalculatePrice_MarginCalculation_Correct()
    {
        // Arrange
        const decimal purchaseCost = 10.00m;
        const decimal markupRate = 50m; // 50% markup
        const decimal discountRate = 0m;
        const decimal vatRate = 22m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(15.00m, result.ListPrice); // 10 * 1.50
        Assert.Equal(15.00m, result.FinalNetPrice);
        Assert.Equal(5.00m, result.MarginAmount); // 15 - 10
        Assert.Equal(33.33m, result.MarginPercent); // (5 / 15) * 100
    }

    #endregion

    #region VAT Breakdown (Castelletto IVA) Tests

    [Fact]
    public void CalculateVatBreakdown_SingleVatRate_CorrectBreakdown()
    {
        // Arrange
        var items = new List<(decimal TaxableAmount, decimal VatRate)>
        {
            (100m, 22m),
            (50m, 22m)
        };

        // Act
        var breakdown = _pricingEngine.CalculateVatBreakdown(items).ToList();

        // Assert
        Assert.Single(breakdown);
        Assert.Equal(22m, breakdown[0].VatRate);
        Assert.Equal(150m, breakdown[0].TaxableAmount);
        Assert.Equal(33.00m, breakdown[0].VatAmount); // 150 * 0.22
    }

    [Fact]
    public void CalculateVatBreakdown_MultipleVatRates_CorrectGrouping()
    {
        // Arrange
        var items = new List<(decimal TaxableAmount, decimal VatRate)>
        {
            (100m, 22m),
            (50m, 10m),
            (75m, 22m),
            (25m, 10m)
        };

        // Act
        var breakdown = _pricingEngine.CalculateVatBreakdown(items).ToList();

        // Assert
        Assert.Equal(2, breakdown.Count);

        var vat10 = breakdown.FirstOrDefault(b => b.VatRate == 10m);
        Assert.NotNull(vat10);
        Assert.Equal(75m, vat10.TaxableAmount); // 50 + 25
        Assert.Equal(7.50m, vat10.VatAmount);

        var vat22 = breakdown.FirstOrDefault(b => b.VatRate == 22m);
        Assert.NotNull(vat22);
        Assert.Equal(175m, vat22.TaxableAmount); // 100 + 75
        Assert.Equal(38.50m, vat22.VatAmount);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CalculatePrice_ZeroPurchaseCost_ValidCalculation()
    {
        // Arrange
        const decimal purchaseCost = 0m;
        const decimal markupRate = 100m;
        const decimal discountRate = 0m;
        const decimal vatRate = 22m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.Equal(0m, result.ListPrice);
        Assert.Equal(0m, result.FinalNetPrice);
        Assert.Equal(0m, result.VatAmount);
    }

    [Fact]
    public void CalculatePrice_LargeValues_AccurateCalculation()
    {
        // Arrange
        const decimal purchaseCost = 999999.99m;
        const decimal markupRate = 25m;
        const decimal discountRate = 15m;
        const decimal vatRate = 22m;

        // Act
        var result = _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);

        // Assert
        Assert.True(result.FinalNetPrice > 0);
        Assert.True(result.VatAmount > 0);
        Assert.True(result.FinalGrossPrice > 0);
    }

    [Fact]
    public void CalculatePrice_InvalidInputs_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _pricingEngine.CalculatePrice(-10, 0, 0, 22)
        );

        Assert.Throws<ArgumentException>(() =>
            _pricingEngine.CalculatePrice(20, -10, 0, 22)
        );

        Assert.Throws<ArgumentException>(() =>
            _pricingEngine.CalculatePrice(20, 0, 101, 22)
        );
    }

    #endregion
}
