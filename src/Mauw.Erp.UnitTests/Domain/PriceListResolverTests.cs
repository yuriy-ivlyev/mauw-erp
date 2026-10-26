using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public class PriceListResolverTests
{
    private static readonly Guid Variant = Guid.NewGuid();
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Category = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly PriceListResolver _resolver = new(new ItalianAccountingCalculator());

    [Theory]
    [InlineData(4, "10")]
    [InlineData(3, "20")]
    [InlineData(2, "30")]
    [InlineData(1, "40")]
    [InlineData(0, "50.12345678")]
    public void CascadeSelectsMostSpecificTargetBeforePriority(int count, string expected)
    {
        PriceRuleDefinition[] rules =
        [
            new(null, PriceCalculationType.FixedPrice, 10m, VariantId: Variant, Priority: -100),
            new(null, PriceCalculationType.FixedPrice, 20m, ProductId: Product, Priority: 100),
            new(null, PriceCalculationType.FixedPrice, 30m, CategoryId: Category, Priority: 200),
            new(null, PriceCalculationType.FixedPrice, 40m, Priority: 300)
        ];
        var list = Create(rules.Skip(4 - count).ToArray());
        var result = _resolver.Resolve(list, Input());

        Assert.True(result.Success, result.Error);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result.Data!.ListUnitPrice);
    }

    [Fact]
    public void QuantityAndInclusiveDatesFilterBeforePriority()
    {
        var list = Create(
            new(null, PriceCalculationType.FixedPrice, 8m, MinimumQuantity: 5m, ValidFrom: At, ValidTo: At, Priority: 10),
            new(null, PriceCalculationType.FixedPrice, 10m));
        Assert.Equal(8m, _resolver.Resolve(list, Input() with { Quantity = 5m }).Data!.ListUnitPrice);
        Assert.Equal(10m, _resolver.Resolve(list, Input() with { Quantity = 4m }).Data!.ListUnitPrice);
        Assert.Equal(10m, _resolver.Resolve(list, Input() with { Quantity = 5m, At = At.AddTicks(1) }).Data!.ListUnitPrice);
    }

    [Fact]
    public void EqualSpecificityAndPriorityFailsInsteadOfSelectingArbitraryRule()
    {
        var list = Create(new(null, PriceCalculationType.FixedPrice, 10m),
            new(null, PriceCalculationType.PercentageDiscount, 5m));
        Assert.False(_resolver.Resolve(list, Input()).Success);
    }

    [Fact]
    public void OtherTargetsNeverMatchAndInactiveListFails()
    {
        var list = Create(new PriceRuleDefinition(null, PriceCalculationType.FixedPrice, 10m, VariantId: Guid.NewGuid()));
        Assert.Equal(Input().BaseUnitPrice, _resolver.Resolve(list, Input()).Data!.ListUnitPrice);
        list.SetActivation(false);
        Assert.False(_resolver.Resolve(list, Input()).Success);
    }

    public static TheoryData<PriceCalculationType, decimal, decimal, decimal> Formulas => new()
    {
        { PriceCalculationType.FixedPrice, 12.12345678m, 12.12345678m, 0m },
        { PriceCalculationType.PercentageDiscount, 10m, 45.11111110m, 10m }
    };

    [Theory]
    [MemberData(nameof(Formulas))]
    public void FormulaPreservesPrecisionAndDiscountIsNotAppliedTwice(
        PriceCalculationType type, decimal value, decimal expectedNet, decimal expectedDiscount)
    {
        var list = Create(new PriceRuleDefinition(null, type, value));
        var result = _resolver.Resolve(list, Input());
        Assert.True(result.Success, result.Error);
        Assert.Equal(expectedNet, result.Data!.AgreedUnitNetPrice);
        Assert.Equal(expectedDiscount, result.Data.DiscountPercent);
        if (type == PriceCalculationType.PercentageDiscount)
            Assert.Equal(50.12345678m, result.Data.ListUnitPrice);
    }

    [Fact]
    public void ListWithFixedPriceAndPercentageDiscountRulesStillResolvesEachRule()
    {
        var list = Create(
            new(null, PriceCalculationType.FixedPrice, 20m, VariantId: Variant),
            new(null, PriceCalculationType.PercentageDiscount, 10m, CategoryId: Category));

        var fixedPrice = _resolver.Resolve(list, Input());
        Assert.True(fixedPrice.Success, fixedPrice.Error);
        Assert.Equal(20m, fixedPrice.Data!.AgreedUnitNetPrice);
        Assert.Equal(0m, fixedPrice.Data.DiscountPercent);

        var discount = _resolver.Resolve(list, Input() with { VariantId = Guid.NewGuid() });
        Assert.True(discount.Success, discount.Error);
        Assert.Equal(50.12345678m, discount.Data!.ListUnitPrice);
        Assert.Equal(10m, discount.Data.DiscountPercent);
        Assert.Equal(45.11111110m, discount.Data.AgreedUnitNetPrice);
    }

    [Fact]
    public void FractionalQuantitiesAreRejected()
    {
        Assert.False(PriceList.Create("Test", "EUR", true,
            [new PriceRuleDefinition(null, PriceCalculationType.FixedPrice, 3.12345678m, MinimumQuantity: 0.25m)]).Success);
        Assert.False(_resolver.Resolve(null, Input() with { Quantity = 0.25m }).Success);
    }

    public static TheoryData<PriceRuleDefinition> InvalidRules => new()
    {
        new(null, PriceCalculationType.FixedPrice, -1m),
        new(null, PriceCalculationType.PercentageDiscount, 100.01m),
        new(null, PriceCalculationType.FixedPrice, 1.123456789m),
        new(null, PriceCalculationType.FixedPrice, 1m, MinimumQuantity: 0m),
        new(null, PriceCalculationType.FixedPrice, 1m, MinimumQuantity: 0.25m),
        new(null, PriceCalculationType.FixedPrice, 1m, VariantId: Variant, ProductId: Product),
        new(null, PriceCalculationType.FixedPrice, 1m, ValidFrom: At, ValidTo: At.AddDays(-1)),
        new(null, (PriceCalculationType)999, 1m),
        new(null, PriceCalculationType.FixedPrice, decimal.MaxValue)
    };

    [Theory]
    [MemberData(nameof(InvalidRules))]
    public void InvalidUpdateDoesNotMutateAggregate(PriceRuleDefinition invalid)
    {
        var list = Create(new PriceRuleDefinition(null, PriceCalculationType.FixedPrice, 12m));
        var original = Assert.Single(list.Rules);
        var timestamp = list.UpdatedAt;
        Assert.False(list.Update("Changed", "EUR", false, [invalid]).Success);
        Assert.Equal("Test", list.Name);
        Assert.True(list.IsActive);
        Assert.Same(original, Assert.Single(list.Rules));
        Assert.Equal(timestamp, list.UpdatedAt);
    }

    [Fact]
    public void InvalidInputsAndOverflowReturnExplicitFailures()
    {
        Assert.False(_resolver.Resolve(null, Input() with { Quantity = 0m }).Success);
        Assert.False(_resolver.Resolve(null, Input() with { BaseUnitPrice = -1m }).Success);

        Assert.False(PriceList.Create("Test", "USD", true, []).Success);
    }

    private static PriceList Create(params PriceRuleDefinition[] rules) => PriceList.Create("Test", "EUR", true, rules).Data!;
    private static PriceResolutionInput Input() => new(Variant, Product, Category, 1m, 50.12345678m, At);
}
