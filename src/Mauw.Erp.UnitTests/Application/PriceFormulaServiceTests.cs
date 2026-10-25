using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class PriceFormulaServiceTests
{
    private readonly Mock<IPriceFormulaRepository> _repository = new(MockBehavior.Strict);

    private PriceFormulaService CreateService() => new(_repository.Object, new PriceFormulaCalculator());

    private static PriceFormula Existing(decimal roundingStep, params decimal[] percentages) =>
        PriceFormula.Create(new PriceFormulaDefinition(
            percentages.Select((p, i) => new PriceFormulaStepDefinition($"Passo {i + 1}", p)).ToList(),
            roundingStep), "old").Data!;

    private static SavePriceFormulaRequest Request(decimal roundingStep, params decimal[] percentages) => new()
    {
        Steps = percentages.Select((p, i) => new PriceFormulaStepDto($"Nuovo {i + 1}", p)).ToList(),
        RoundingStep = roundingStep
    };

    public static TheoryData<decimal> InvalidPercentages => new() { -0.01m, 9999.01m, 1.001m };

    public static TheoryData<decimal> InvalidCosts => new() { -0.01m, 10.005m };

    [Fact]
    public async Task GetWithoutFormulaReturnsTheUnconfiguredDefault()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((PriceFormula?)null);

        var dto = await CreateService().GetAsync(CancellationToken.None);

        Assert.False(dto.IsConfigured);
        Assert.Empty(dto.Steps);
        Assert.Equal(0.10m, dto.RoundingStep);
        Assert.Null(dto.UpdatedAt);
        Assert.Null(dto.UpdatedByUserId);
    }

    [Fact]
    public async Task SaveCreatesTheFormulaWhenAbsent()
    {
        PriceFormula? added = null;
        _repository.Setup(r => r.GetForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync((PriceFormula?)null);
        _repository.Setup(r => r.AddAsync(It.IsAny<PriceFormula>(), It.IsAny<CancellationToken>()))
            .Callback<PriceFormula, CancellationToken>((formula, _) => added = formula).Returns(Task.CompletedTask);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().SaveAsync(Request(0.10m, 40m, 10m), "admin-1", CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(added);
        Assert.Equal("admin-1", result.Data!.UpdatedByUserId);
        Assert.Equal([40m, 10m], result.Data.Steps.Select(step => step.Percentage));
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveUpdatesTheTrackedFormulaWhenPresent()
    {
        var existing = Existing(0.50m, 1m, 2m, 3m);
        _repository.Setup(r => r.GetForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().SaveAsync(Request(0.05m, 25m), "admin-2", CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(0.05m, existing.RoundingStep);
        Assert.Equal("admin-2", existing.UpdatedByUserId);
        Assert.Equal([25m], existing.Steps.Select(step => step.Percentage));
        _repository.Verify(r => r.AddAsync(It.IsAny<PriceFormula>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(InvalidPercentages))]
    public async Task InvalidSaveFailsBeforeTouchingTheRepository(decimal percentage)
    {
        var result = await CreateService().SaveAsync(Request(0.10m, percentage), "admin", CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveWithNullStepsFailsWithoutThrowing()
    {
        var result = await CreateService().SaveAsync(new SavePriceFormulaRequest { Steps = null! }, "admin", CancellationToken.None);

        Assert.False(result.Success);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CalculateWithoutFormulaFailsExplicitlyAndNeverSaves()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((PriceFormula?)null);

        var result = await CreateService().CalculateAsync(20.00m, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Formula prezzi non configurata: impostarla in Impostazioni > Formula prezzi.", result.Error);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CalculateUsesTheNoTrackingReadAndNeverSaves()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Existing(0.10m, 40m, 10m, 5m));

        var result = await CreateService().CalculateAsync(20.00m, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(32.34m, result.Data!.UnroundedPrice);
        Assert.Equal(32.40m, result.Data.FinalPrice);
        Assert.Equal([28.00m, 30.80m, 32.34m], result.Data.Steps.Select(step => step.RunningValue));
        _repository.Verify(r => r.GetForUpdateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<PriceFormula>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(InvalidCosts))]
    public async Task CalculateRejectsInvalidCostsWithAResult(decimal cost)
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Existing(0.10m, 40m));

        var result = await CreateService().CalculateAsync(cost, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("costo d'acquisto", result.Error);
    }
}
