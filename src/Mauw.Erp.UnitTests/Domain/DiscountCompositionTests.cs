using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class DiscountCompositionTests
{
    private readonly ItalianAccountingCalculator _calculator = new();

    private static DiscountInput Percent(decimal value) => new(value, null);
    private static DiscountInput Amount(decimal value) => new(null, value);

    [Fact]
    public void PercentagesCompoundOnWhatTheEarlierDiscountsLeft()
    {
        var result = _calculator.CombineDiscounts(1m, 100m, [Percent(5m), Percent(3m), Percent(5m)]);

        Assert.True(result.Success, result.Error);
        Assert.Equal(12.4575m, result.Data!.EffectivePercent);
        Assert.Equal(new[] { 5.00m, 2.85m, 4.61m }, result.Data.SavedAmounts);
        var line = _calculator.CalculateLine(new(1m, 100m, 22m, null, result.Data.EffectivePercent));
        Assert.Equal(87.54m, line.Data!.LineNetTotal);
    }

    [Fact]
    public void FixedAmountIsADiscountOnTheLineTotalAndKeepsTheCentsExact()
    {
        var result = _calculator.CombineDiscounts(5m, 18.84m, [Amount(4.20m)]);

        Assert.True(result.Success, result.Error);
        Assert.Equal(new[] { 4.20m }, result.Data!.SavedAmounts);
        var line = _calculator.CalculateLine(new(5m, 18.84m, 22m, null, result.Data.EffectivePercent));
        Assert.Equal(90.00m, line.Data!.LineNetTotal);
    }

    [Fact]
    public void AmountAfterPercentageIsTakenFromWhatIsLeft()
    {
        var result = _calculator.CombineDiscounts(2m, 10m, [Percent(10m), Amount(2.00m)]);

        Assert.True(result.Success, result.Error);
        Assert.Equal(20m, result.Data!.EffectivePercent);
        Assert.Equal(new[] { 2.00m, 2.00m }, result.Data.SavedAmounts);
    }

    [Fact]
    public void EffectivePercentIsRoundedOnceToTenDecimalsAwayFromZero()
    {
        var result = _calculator.CombineDiscounts(1m, 3m, [Amount(1.00m)]);

        Assert.True(result.Success, result.Error);
        Assert.Equal(33.3333333333m, result.Data!.EffectivePercent);
    }

    [Fact]
    public void AmountEqualToTheLineTotalIsAFullDiscountAndNoDiscountsMeansZero()
    {
        var full = _calculator.CombineDiscounts(5m, 18.84m, [Amount(94.20m)]);
        var none = _calculator.CombineDiscounts(5m, 18.84m, []);
        var free = _calculator.CombineDiscounts(5m, 0m, [Percent(10m)]);

        Assert.Equal(100m, full.Data!.EffectivePercent);
        Assert.Equal(0m, none.Data!.EffectivePercent);
        Assert.Empty(none.Data.SavedAmounts);
        Assert.Equal(0m, free.Data!.EffectivePercent);
    }

    [Fact]
    public void FractionalQuantityAndPriceWithEightDecimalsAreSupported()
    {
        var result = _calculator.CombineDiscounts(0.5m, 17.12345678m, [Percent(10m), Amount(0.50m)]);

        Assert.True(result.Success, result.Error);
        var line = _calculator.CalculateLine(new(0.5m, 17.12345678m, 22m, null, result.Data!.EffectivePercent));
        Assert.Equal(7.21m, line.Data!.LineNetTotal);
    }

    [Fact]
    public void InvalidInputsReturnErrorsAndNeverThrow()
    {
        Assert.False(_calculator.CombineDiscounts(1m, 10m, null).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [null!]).Success);
        Assert.False(_calculator.CombineDiscounts(0m, 10m, [Percent(5m)]).Success);
        Assert.False(_calculator.CombineDiscounts(-1m, 10m, [Percent(5m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, -1m, [Percent(5m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10.123456789m, [Percent(5m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [new DiscountInput(null, null)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [new DiscountInput(5m, 1m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [Percent(100.01m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [Percent(-0.01m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [Amount(-1m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [Amount(1.005m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [Amount(10.01m)]).Success);
        Assert.False(_calculator.CombineDiscounts(1m, 10m, [Percent(50m), Amount(5.01m)]).Success);
        Assert.False(_calculator.CombineDiscounts(decimal.MaxValue, 2m, [Percent(5m)]).Success);
    }

    [Fact]
    public void CalculationIsDeterministicAndDoesNotMutateItsInput()
    {
        IReadOnlyList<DiscountInput> discounts = [Percent(5m), Amount(1.00m)];

        var first = _calculator.CombineDiscounts(3m, 7.77m, discounts);
        var second = _calculator.CombineDiscounts(3m, 7.77m, discounts);

        Assert.Equal(first.Data!.EffectivePercent, second.Data!.EffectivePercent);
        Assert.Equal(first.Data.SavedAmounts, second.Data.SavedAmounts);
        Assert.Equal(new[] { Percent(5m), Amount(1.00m) }, discounts);
    }

    [Fact]
    public void TwoLinesOfZeroPointZeroThreeKeepTheSingleVatGroupRule()
    {
        var effective = _calculator.CombineDiscounts(1m, 0.03m, [Percent(0m)]);
        var line = new DocumentLineInput(1m, 0.03m, 22m, null, effective.Data!.EffectivePercent);

        var document = _calculator.Calculate([line, line]);

        Assert.Equal(0.06m, document.Data!.TotalTaxable);
        Assert.Equal(0.01m, document.Data.TotalVat);
    }
}
