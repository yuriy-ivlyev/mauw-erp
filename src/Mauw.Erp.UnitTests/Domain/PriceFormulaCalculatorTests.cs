using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public class PriceFormulaCalculatorTests
{
    private readonly PriceFormulaCalculator _calculator = new();

    private static PriceFormulaDefinition Formula(decimal roundingStep, params decimal[] percentages) =>
        new(percentages.Select((p, i) => new PriceFormulaStepDefinition($"Passo {i + 1}", p)).ToList(), roundingStep);

    [Fact]
    public void CascadeAppliesEachPercentageToThePreviousResultAndRoundsUp()
    {
        var result = _calculator.Calculate(20.00m, Formula(0.10m, 40m, 10m, 5m));

        Assert.True(result.Success, result.Error);
        var data = result.Data!;
        Assert.Equal(20.00m, data.PurchaseCost);
        Assert.Equal([28.00m, 30.80m, 32.34m], data.Steps.Select(s => s.RunningValue));
        Assert.Equal(32.34m, data.UnroundedPrice);
        Assert.Equal(0.10m, data.RoundingStep);
        Assert.Equal(32.40m, data.FinalPrice);
        Assert.Equal(2, decimal.GetBits(data.FinalPrice)[3] >> 16 & 0xFF);
    }

    // Costo 1.00 con un unico passo 4457.89% (limite ammesso 9999): 1.00 * 45.5789 = 45.5789 esatto.
    [Fact]
    public void RoundingIsAlwaysUpNeverToNearest()
    {
        var result = _calculator.Calculate(1.00m, Formula(0.10m, 4457.89m));

        Assert.True(result.Success, result.Error);
        Assert.Equal(45.5789m, result.Data!.UnroundedPrice);
        Assert.Equal(45.5789m, result.Data.Steps[0].RunningValue);
        Assert.Equal(45.60m, result.Data.FinalPrice);
    }

    [Fact]
    public void SingleStepJustAboveHalfStepAlsoRoundsUp()
    {
        var result = _calculator.Calculate(29.60m, Formula(0.10m, 54m));
        Assert.Equal(45.584m, result.Data!.UnroundedPrice);
        Assert.Equal(45.60m, result.Data.FinalPrice);
    }

    public static TheoryData<decimal, decimal, decimal, decimal> RoundingCases => new()
    {
        { 0.10m, 45.60m, 0m, 45.60m },
        { 0.10m, 45.50m, 0m, 45.50m },
        { 0.10m, 45.51m, 0m, 45.60m },
        { 0.05m, 10.00m, 3.33m, 10.35m },
        { 0.05m, 10.00m, 2.50m, 10.25m },
        { 0.05m, 10.00m, 0m, 10.00m },
        { 1.00m, 10.01m, 0m, 11.00m },
        { 1.00m, 10.00m, 0m, 10.00m },
        { 10.00m, 20.01m, 0m, 30.00m },
        { 10.00m, 20.00m, 0m, 20.00m },
        { 100.00m, 100.01m, 0m, 200.00m },
        { 0.03m, 1.00m, 0m, 1.02m }
    };

    [Theory]
    [MemberData(nameof(RoundingCases))]
    public void FinalPriceIsTheNextMultipleOfTheStep(decimal step, decimal cost, decimal percentage, decimal expected)
    {
        var result = _calculator.Calculate(cost, Formula(step, percentage));
        Assert.True(result.Success, result.Error);
        Assert.Equal(expected, result.Data!.FinalPrice);
    }

    [Fact]
    public void FinalPriceHasTwoDecimalsEvenWithStepsWrittenWithFewerDecimals()
    {
        foreach (var step in new[] { 0.1m, 1m, 10m })
        {
            var zero = _calculator.Calculate(0m, Formula(step, 10m)).Data!.FinalPrice;
            var nonZero = _calculator.Calculate(20m, Formula(step, 10m)).Data!.FinalPrice;
            Assert.Equal(2, decimal.GetBits(zero)[3] >> 16 & 0xFF);
            Assert.Equal(2, decimal.GetBits(nonZero)[3] >> 16 & 0xFF);
        }
    }

    [Fact]
    public void ZeroCostGivesMonetaryZeroWithTwoDecimals()
    {
        var result = _calculator.Calculate(0m, Formula(0.10m, 40m, 10m));
        Assert.True(result.Success, result.Error);
        Assert.Equal(0.00m, result.Data!.FinalPrice);
        Assert.Equal(2, decimal.GetBits(result.Data.FinalPrice)[3] >> 16 & 0xFF);
        Assert.All(result.Data.Steps, s => Assert.Equal(0m, s.RunningValue));
    }

    [Fact]
    public void ZeroPercentageStepsKeepTheValueAndAreReported()
    {
        var result = _calculator.Calculate(12.34m, Formula(0.10m, 0m, 0m, 0m));
        Assert.True(result.Success, result.Error);
        Assert.Equal(3, result.Data!.Steps.Count);
        Assert.All(result.Data.Steps, s => Assert.Equal(12.34m, s.RunningValue));
        Assert.Equal(12.40m, result.Data.FinalPrice);
    }

    [Fact]
    public void MaximumNumberOfStepsIsAccepted()
    {
        var result = _calculator.Calculate(10.00m, Formula(0.01m, Enumerable.Repeat(1m, 20).ToArray()));
        Assert.True(result.Success, result.Error);
        Assert.Equal(20, result.Data!.Steps.Count);
        var expected = 10.00m;
        foreach (var step in result.Data.Steps)
        {
            expected *= 1.01m;
            Assert.Equal(expected, step.RunningValue);
        }
        Assert.Equal(decimal.Ceiling(expected * 100m) / 100m, result.Data.FinalPrice);
    }

    [Fact]
    public void MaxCostIsAcceptedWhenFinalPriceFits()
    {
        var result = _calculator.Calculate(99999999999999.99m, Formula(0.01m, 0m));
        Assert.True(result.Success, result.Error);
        Assert.Equal(99999999999999.99m, result.Data!.FinalPrice);
    }

    [Fact]
    public void FinalPriceAbovePersistencePrecisionFails()
    {
        // 99999999999999.99 arrotondato per eccesso a 0.10 diventa 100000000000000.00
        var result = _calculator.Calculate(99999999999999.99m, Formula(0.10m, 0m));
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public void DecimalOverflowReturnsFailureWithoutThrowing()
    {
        var result = _calculator.Calculate(99999999999999.99m, Formula(0.10m, Enumerable.Repeat(9999m, 10).ToArray()));
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error);
    }

    public static TheoryData<decimal> InvalidCosts => new()
        { -0.01m, -1m, 1.001m, 0.005m, 100000000000000.00m };

    [Theory]
    [MemberData(nameof(InvalidCosts))]
    public void InvalidPurchaseCostFails(decimal cost)
    {
        var result = _calculator.Calculate(cost, Formula(0.10m, 10m));
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.NotEmpty(result.Error);
    }

    public static TheoryData<decimal> InvalidPercentages => new()
        { -0.01m, -5m, 9999.01m, 10000m, 10.001m };

    [Theory]
    [MemberData(nameof(InvalidPercentages))]
    public void InvalidPercentageFails(decimal percentage)
    {
        var formula = Formula(0.10m, percentage);
        Assert.False(_calculator.Calculate(10m, formula).Success);
        Assert.False(_calculator.Validate(formula).Success);
    }

    public static TheoryData<decimal> BoundaryPercentages => new() { 0m, 9999m, 10.55m };

    [Theory]
    [MemberData(nameof(BoundaryPercentages))]
    public void BoundaryPercentagesAreValid(decimal percentage) =>
        Assert.True(_calculator.Validate(Formula(0.10m, percentage)).Success);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyLabelFails(string? label)
    {
        var formula = new PriceFormulaDefinition([new PriceFormulaStepDefinition(label!, 10m)], 0.10m);
        Assert.False(_calculator.Calculate(10m, formula).Success);
        Assert.False(_calculator.Validate(formula).Success);
    }

    [Fact]
    public void LabelLengthIsLimitedToOneHundredCharactersAfterTrim()
    {
        var ok = new PriceFormulaDefinition(
            [new PriceFormulaStepDefinition("  " + new string('a', 100) + "  ", 10m)], 0.10m);
        var tooLong = new PriceFormulaDefinition(
            [new PriceFormulaStepDefinition(new string('a', 101), 10m)], 0.10m);

        Assert.True(_calculator.Calculate(10m, ok).Success);
        Assert.Equal(new string('a', 100), _calculator.Calculate(10m, ok).Data!.Steps[0].Label);
        Assert.False(_calculator.Calculate(10m, tooLong).Success);
        Assert.False(_calculator.Validate(tooLong).Success);
    }

    [Fact]
    public void StepCountMustBeBetweenOneAndTwenty()
    {
        var empty = Formula(0.10m);
        var tooMany = Formula(0.10m, Enumerable.Repeat(1m, 21).ToArray());

        foreach (var formula in new[] { empty, tooMany })
        {
            Assert.False(_calculator.Calculate(10m, formula).Success);
            Assert.False(_calculator.Validate(formula).Success);
        }
        Assert.True(_calculator.Validate(Formula(0.10m, 1m)).Success);
        Assert.True(_calculator.Validate(Formula(0.10m, Enumerable.Repeat(1m, 20).ToArray())).Success);
    }

    public static TheoryData<decimal> InvalidRoundingSteps => new()
        { 0m, -0.10m, 0.001m, 0.105m, 100.01m };

    [Theory]
    [MemberData(nameof(InvalidRoundingSteps))]
    public void InvalidRoundingStepFails(decimal step)
    {
        var formula = Formula(step, 10m);
        Assert.False(_calculator.Calculate(10m, formula).Success);
        Assert.False(_calculator.Validate(formula).Success);
    }

    public static TheoryData<decimal> BoundaryRoundingSteps => new() { 0.01m, 0.10m, 100.00m };

    [Theory]
    [MemberData(nameof(BoundaryRoundingSteps))]
    public void BoundaryRoundingStepsAreValid(decimal step) =>
        Assert.True(_calculator.Validate(Formula(step, 10m)).Success);

    [Fact]
    public void NullFormulaOrStepsFailWithoutThrowing()
    {
        Assert.False(_calculator.Calculate(10m, null!).Success);
        Assert.False(_calculator.Validate(null!).Success);
        Assert.False(_calculator.Validate(new PriceFormulaDefinition(null!, 0.10m)).Success);
        Assert.False(_calculator.Validate(new PriceFormulaDefinition([null!], 0.10m)).Success);
    }

    [Fact]
    public void BreakdownOrderEqualsStepOrder()
    {
        var formula = new PriceFormulaDefinition(
        [
            new("Ricarico", 30m), new("Provvigione", 8m), new("Trasporto", 2m), new("Extra", 0m)
        ], 0.10m);

        var data = _calculator.Calculate(100m, formula).Data!;

        Assert.Equal(["Ricarico", "Provvigione", "Trasporto", "Extra"], data.Steps.Select(s => s.Label));
        Assert.Equal([30m, 8m, 2m, 0m], data.Steps.Select(s => s.Percentage));
        Assert.Equal([130m, 140.4m, 143.208m, 143.208m], data.Steps.Select(s => s.RunningValue));
        Assert.Equal(143.30m, data.FinalPrice);
    }

    [Fact]
    public void CalculateDoesNotMutateInputsOnSuccessOrFailure()
    {
        var steps = new List<PriceFormulaStepDefinition> { new("  Ricarico ", 40m), new("Provvigione", 10m) };
        var formula = new PriceFormulaDefinition(steps, 0.10m);

        _calculator.Calculate(20m, formula);
        _calculator.Calculate(-1m, formula);

        Assert.Equal(0.10m, formula.RoundingStep);
        Assert.Same(steps, formula.Steps);
        Assert.Equal(2, steps.Count);
        Assert.Equal(new PriceFormulaStepDefinition("  Ricarico ", 40m), steps[0]);
        Assert.Equal(new PriceFormulaStepDefinition("Provvigione", 10m), steps[1]);

        var invalidSteps = new List<PriceFormulaStepDefinition> { new("X", 10000m) };
        var invalid = new PriceFormulaDefinition(invalidSteps, 0.10m);
        Assert.False(_calculator.Calculate(20m, invalid).Success);
        Assert.Single(invalidSteps);
        Assert.Equal(10000m, invalidSteps[0].Percentage);
    }

    [Fact]
    public void ResultIsNotAffectedByLaterChangesToTheInputList()
    {
        var steps = new List<PriceFormulaStepDefinition> { new("Ricarico", 40m) };
        var data = _calculator.Calculate(20m, new PriceFormulaDefinition(steps, 0.10m)).Data!;

        steps.Add(new("Altro", 50m));
        steps[0] = new("Cambiato", 99m);

        Assert.Single(data.Steps);
        Assert.Equal("Ricarico", data.Steps[0].Label);
        Assert.IsNotType<List<PriceFormulaStepResult>>(data.Steps);
    }

    [Fact]
    public void CalculationIsDeterministic()
    {
        var formula = Formula(0.10m, 33.33m, 7.77m, 12.5m);

        var first = _calculator.Calculate(19.99m, formula).Data!;
        var second = new PriceFormulaCalculator().Calculate(19.99m, formula).Data!;

        Assert.Equal(first.PurchaseCost, second.PurchaseCost);
        Assert.Equal(first.UnroundedPrice, second.UnroundedPrice);
        Assert.Equal(first.RoundingStep, second.RoundingStep);
        Assert.Equal(first.FinalPrice, second.FinalPrice);
        Assert.Equal(first.Steps, second.Steps);
    }

    [Fact]
    public void ValidateReturnsNormalizedCopyAndAgreesWithCalculate()
    {
        var steps = new List<PriceFormulaStepDefinition> { new("  Ricarico ", 40m) };
        var validated = _calculator.Validate(new PriceFormulaDefinition(steps, 0.10m));

        Assert.True(validated.Success, validated.Error);
        Assert.Equal("Ricarico", validated.Data!.Steps[0].Label);
        Assert.Equal(0.10m, validated.Data.RoundingStep);
        Assert.Equal("  Ricarico ", steps[0].Label);

        var bad = Formula(0.10m, -1m);
        Assert.Equal(_calculator.Validate(bad).Error, _calculator.Calculate(10m, bad).Error);
    }
}
