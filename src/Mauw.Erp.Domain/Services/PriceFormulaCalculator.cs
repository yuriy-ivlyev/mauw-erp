using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Domain.Services;

/// <summary>
/// Formula prezzi: costo d'acquisto netto IVA, passi percentuali a cascata (non arrotondati)
/// e arrotondamento finale sempre per eccesso a un multiplo del passo. Nessun calcolo IVA.
/// </summary>
public sealed class PriceFormulaCalculator
{
    public const int MaxSteps = 20;
    public const int MaxLabelLength = 100;
    public const decimal MaxPurchaseCost = 99999999999999.99m;
    public const decimal MaxPercentage = 9999m;
    public const decimal MaxRoundingStep = 100.00m;
    public const decimal MaxFinalPrice = 99999999999999.99999999m;

    public AccountingResult<PriceFormulaDefinition> Validate(PriceFormulaDefinition formula)
    {
        if (formula?.Steps is null)
            return AccountingResult<PriceFormulaDefinition>.Fail("Formula prezzi mancante.");
        if (formula.Steps.Count is < 1 or > MaxSteps)
            return AccountingResult<PriceFormulaDefinition>.Fail(
                $"La formula prezzi deve avere da 1 a {MaxSteps} passaggi.");
        if (formula.RoundingStep <= 0m || formula.RoundingStep > MaxRoundingStep || !HasMaxDecimals(formula.RoundingStep, 2))
            return AccountingResult<PriceFormulaDefinition>.Fail(
                "Il passo di arrotondamento deve essere maggiore di zero, al massimo 100.00 e con massimo 2 decimali.");

        var steps = new List<PriceFormulaStepDefinition>(formula.Steps.Count);
        foreach (var step in formula.Steps)
        {
            var label = step?.Label?.Trim();
            if (string.IsNullOrEmpty(label))
                return AccountingResult<PriceFormulaDefinition>.Fail("Ogni passaggio della formula richiede una descrizione.");
            if (label.Length > MaxLabelLength)
                return AccountingResult<PriceFormulaDefinition>.Fail(
                    $"La descrizione del passaggio non puo superare {MaxLabelLength} caratteri.");
            if (step!.Percentage is < 0m or > MaxPercentage || !HasMaxDecimals(step.Percentage, 2))
                return AccountingResult<PriceFormulaDefinition>.Fail(
                    $"La percentuale del passaggio '{label}' deve essere da 0 a 9999 con massimo 2 decimali.");
            steps.Add(new PriceFormulaStepDefinition(label, step.Percentage));
        }

        return AccountingResult<PriceFormulaDefinition>.Ok(
            new PriceFormulaDefinition(steps.AsReadOnly(), formula.RoundingStep));
    }

    public AccountingResult<PriceFormulaResult> Calculate(decimal purchaseCost, PriceFormulaDefinition formula)
    {
        if (purchaseCost < 0m || purchaseCost > MaxPurchaseCost || !HasMaxDecimals(purchaseCost, 2))
            return AccountingResult<PriceFormulaResult>.Fail(
                "Il costo d'acquisto deve essere non negativo, al massimo 99999999999999.99 e con massimo 2 decimali.");

        var validation = Validate(formula);
        if (!validation.Success)
            return AccountingResult<PriceFormulaResult>.Fail(validation.Error);

        try
        {
            var definition = validation.Data!;
            var running = purchaseCost;
            var results = new List<PriceFormulaStepResult>(definition.Steps.Count);
            foreach (var step in definition.Steps)
            {
                running *= 1m + step.Percentage / 100m;
                results.Add(new PriceFormulaStepResult(step.Label, step.Percentage, running));
            }

            // "+ 0.00m" garantisce esattamente 2 decimali anche con passo scritto con meno decimali
            var finalPrice = decimal.Round(
                Math.Ceiling(running / definition.RoundingStep) * definition.RoundingStep,
                2, MidpointRounding.AwayFromZero) + 0.00m;
            if (finalPrice > MaxFinalPrice)
                return AccountingResult<PriceFormulaResult>.Fail(
                    "Il prezzo finale supera la precisione massima consentita (14 cifre intere e 8 decimali).");

            return AccountingResult<PriceFormulaResult>.Ok(new PriceFormulaResult(
                purchaseCost, results.AsReadOnly(), running, definition.RoundingStep, finalPrice));
        }
        catch (OverflowException)
        {
            return AccountingResult<PriceFormulaResult>.Fail("Il calcolo della formula prezzi supera la capacita decimal.");
        }
    }

    private static bool HasMaxDecimals(decimal value, int decimals) =>
        decimal.Round(value, decimals, MidpointRounding.AwayFromZero) == value;
}
