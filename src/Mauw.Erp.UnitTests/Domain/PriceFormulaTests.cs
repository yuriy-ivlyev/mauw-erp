using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.UnitTests.Domain;

public class PriceFormulaTests
{
    private static PriceFormulaDefinition Formula(decimal roundingStep, params (string Label, decimal Percentage)[] steps) =>
        new(steps.Select(step => new PriceFormulaStepDefinition(step.Label, step.Percentage)).ToList(), roundingStep);

    private static PriceFormula Valid() =>
        PriceFormula.Create(Formula(0.10m, ("Ricarico", 40.00m), ("Provvigione", 10.00m), ("Sconto", 5.00m)), "admin").Data!;

    [Fact]
    public void CreateAssignsPositionsFromListOrderAndNormalizesInput()
    {
        var result = PriceFormula.Create(
            Formula(0.10m, ("  Ricarico  ", 40.00m), ("Provvigione", 10.00m), ("Sconto", 5.00m)), "  admin  ");

        Assert.True(result.Success, result.Error);
        var formula = result.Data!;
        Assert.NotEqual(Guid.Empty, formula.Id);
        Assert.Equal(0.10m, formula.RoundingStep);
        Assert.Equal("admin", formula.UpdatedByUserId);
        Assert.Equal([1, 2, 3], formula.Steps.Select(step => step.Position));
        Assert.Equal(["Ricarico", "Provvigione", "Sconto"], formula.Steps.Select(step => step.Label));
        Assert.Equal([40.00m, 10.00m, 5.00m], formula.Steps.Select(step => step.Percentage));
        Assert.All(formula.Steps, step => Assert.Equal(formula.Id, step.FormulaId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankUserIdIsStoredAsNull(string? userId)
    {
        var result = PriceFormula.Create(Formula(0.10m, ("Ricarico", 40.00m)), userId);

        Assert.True(result.Success, result.Error);
        Assert.Null(result.Data!.UpdatedByUserId);
    }

    [Fact]
    public void UpdateReplacesAllStepsAtomicallyAndKeepsIdentity()
    {
        var formula = Valid();
        var id = formula.Id;
        var oldStepIds = formula.Steps.Select(step => step.Id).ToList();

        var result = formula.Update(Formula(0.50m, ("Nuovo", 25.50m), ("Altro", 0.00m)), "other");

        Assert.True(result.Success, result.Error);
        Assert.Same(formula, result.Data);
        Assert.Equal(id, formula.Id);
        Assert.Equal(0.50m, formula.RoundingStep);
        Assert.Equal("other", formula.UpdatedByUserId);
        Assert.Equal([1, 2], formula.Steps.Select(step => step.Position));
        Assert.Equal(["Nuovo", "Altro"], formula.Steps.Select(step => step.Label));
        Assert.Equal([25.50m, 0.00m], formula.Steps.Select(step => step.Percentage));
        Assert.Empty(formula.Steps.Select(step => step.Id).Intersect(oldStepIds));
    }

    public static TheoryData<PriceFormulaDefinition> InvalidDefinitions => new()
    {
        Formula(0.10m),
        Formula(0.00m, ("Ricarico", 40.00m)),
        Formula(-0.10m, ("Ricarico", 40.00m)),
        Formula(0.123m, ("Ricarico", 40.00m)),
        Formula(0.10m, ("", 40.00m)),
        Formula(0.10m, ("Ricarico", -0.01m)),
        Formula(0.10m, ("Ricarico", 9999.01m)),
        Formula(0.10m, ("Ricarico", 10.005m)),
        Formula(0.10m, ("Valido", 10.00m), ("Ricarico", 10000m)),
        Formula(0.10m, ("Valido", 10.00m), (new string('x', 101), 10.00m)),
        new(null!, 0.10m)
    };

    [Theory]
    [MemberData(nameof(InvalidDefinitions))]
    public void InvalidUpdateFailsAndLeavesStateUnchanged(PriceFormulaDefinition invalid)
    {
        var formula = Valid();
        var stepIds = formula.Steps.Select(step => step.Id).ToList();
        var updatedAt = formula.UpdatedAt;

        var result = formula.Update(invalid, "intruder");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(0.10m, formula.RoundingStep);
        Assert.Equal("admin", formula.UpdatedByUserId);
        Assert.Equal(stepIds, formula.Steps.Select(step => step.Id));
        Assert.Equal([1, 2, 3], formula.Steps.Select(step => step.Position));
        Assert.Equal([40.00m, 10.00m, 5.00m], formula.Steps.Select(step => step.Percentage));
        Assert.Equal(updatedAt, formula.UpdatedAt);
    }

    [Fact]
    public void TooLongUserIdFailsWithoutMutation()
    {
        var formula = Valid();

        var result = formula.Update(Formula(0.50m, ("Nuovo", 1.00m)), new string('u', 201));

        Assert.False(result.Success);
        Assert.Equal(0.10m, formula.RoundingStep);
        Assert.Equal(3, formula.Steps.Count);
    }

    [Fact]
    public void CreateWithInvalidDefinitionReturnsFailure()
    {
        var result = PriceFormula.Create(Formula(0.10m), "admin");

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void ToDefinitionRoundTripsAndFeedsTheCalculator()
    {
        var formula = Valid();

        var calculation = new Mauw.Erp.Domain.Services.PriceFormulaCalculator().Calculate(20.00m, formula.ToDefinition());

        Assert.True(calculation.Success, calculation.Error);
        Assert.Equal(32.40m, calculation.Data!.FinalPrice);
    }
}
