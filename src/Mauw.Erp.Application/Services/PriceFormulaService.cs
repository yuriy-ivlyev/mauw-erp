using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

public sealed class PriceFormulaService(IPriceFormulaRepository repository, PriceFormulaCalculator calculator)
    : IPriceFormulaService
{
    public const decimal DefaultRoundingStep = 0.10m;
    public const string NotConfiguredMessage =
        "Formula prezzi non configurata: impostarla in Impostazioni > Formula prezzi.";

    public async Task<PriceFormulaDto> GetAsync(CancellationToken ct)
    {
        var formula = await repository.GetAsync(ct);
        return formula is null ? new(false, [], DefaultRoundingStep, null, null) : Map(formula);
    }

    public async Task<OperationResult<PriceFormulaDto>> SaveAsync(
        SavePriceFormulaRequest request, string? userId, CancellationToken ct)
    {
        if (request?.Steps is null || request.Steps.Any(step => step is null))
            return OperationResult<PriceFormulaDto>.Fail("I passaggi della formula prezzi non possono essere null.");
        var definition = new PriceFormulaDefinition(
            request.Steps.Select(step => new PriceFormulaStepDefinition(step.Label, step.Percentage)).ToList(),
            request.RoundingStep);

        // Validazione completa prima di leggere o modificare l'aggregato.
        var validation = calculator.Validate(definition);
        if (!validation.Success)
            return OperationResult<PriceFormulaDto>.Fail(validation.Error!);

        var formula = await repository.GetForUpdateAsync(ct);
        if (formula is null)
        {
            var created = PriceFormula.Create(validation.Data!, userId);
            if (!created.Success)
                return OperationResult<PriceFormulaDto>.Fail(created.Error!);
            formula = created.Data!;
            await repository.AddAsync(formula, ct);
        }
        else
        {
            var updated = formula.Update(validation.Data!, userId);
            if (!updated.Success)
                return OperationResult<PriceFormulaDto>.Fail(updated.Error!);
        }

        await repository.SaveChangesAsync(ct);
        return OperationResult<PriceFormulaDto>.Ok(Map(formula));
    }

    public async Task<OperationResult<PriceFormulaCalculationDto>> CalculateAsync(
        decimal purchaseCost, CancellationToken ct)
    {
        var formula = await repository.GetAsync(ct);
        if (formula is null)
            return OperationResult<PriceFormulaCalculationDto>.Fail(NotConfiguredMessage);

        var result = calculator.Calculate(purchaseCost, formula.ToDefinition());
        if (!result.Success)
            return OperationResult<PriceFormulaCalculationDto>.Fail(result.Error!);

        var data = result.Data!;
        return OperationResult<PriceFormulaCalculationDto>.Ok(new(
            data.PurchaseCost,
            data.Steps.Select(step => new PriceFormulaCalculationStepDto(step.Label, step.Percentage, step.RunningValue))
                .ToList(),
            data.UnroundedPrice, data.RoundingStep, data.FinalPrice));
    }

    // PostgreSQL conserva i microsecondi: stesso valore nella risposta di salvataggio e nelle letture successive.
    private static PriceFormulaDto Map(PriceFormula formula) => new(
        true,
        formula.ToDefinition().Steps.Select(step => new PriceFormulaStepDto(step.Label, step.Percentage)).ToList(),
        formula.RoundingStep,
        new DateTime(formula.UpdatedAt.Ticks - formula.UpdatedAt.Ticks % 10, formula.UpdatedAt.Kind),
        formula.UpdatedByUserId);
}
