namespace Mauw.Erp.Domain.Pricing;

/// <summary>Singolo passo percentuale a cascata, al netto di IVA (es. ricarico, provvigione).</summary>
public sealed record PriceFormulaStepDefinition(string Label, decimal Percentage);

public sealed record PriceFormulaDefinition(
    IReadOnlyList<PriceFormulaStepDefinition> Steps,
    decimal RoundingStep);

public sealed record PriceFormulaStepResult(
    string Label,
    decimal Percentage,
    decimal RunningValue);

public sealed record PriceFormulaResult(
    decimal PurchaseCost,
    IReadOnlyList<PriceFormulaStepResult> Steps,
    decimal UnroundedPrice,
    decimal RoundingStep,
    decimal FinalPrice);
