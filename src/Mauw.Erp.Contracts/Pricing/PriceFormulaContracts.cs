namespace Mauw.Erp.Contracts.Pricing;

/// <summary>Passaggio percentuale a cascata della formula prezzi (al netto di IVA).</summary>
public sealed record PriceFormulaStepDto(string Label, decimal Percentage);

/// <summary>Formula prezzi globale; se non configurata IsConfigured e false e RoundingStep vale 0.10.</summary>
public sealed record PriceFormulaDto(
    bool IsConfigured,
    IReadOnlyList<PriceFormulaStepDto> Steps,
    decimal RoundingStep,
    DateTime? UpdatedAt,
    string? UpdatedByUserId);

public sealed record SavePriceFormulaRequest
{
    public IReadOnlyList<PriceFormulaStepDto> Steps { get; init; } = [];
    public decimal RoundingStep { get; init; } = 0.10m;
}

public sealed record CalculatePriceRequest
{
    /// <summary>Costo d'acquisto netto IVA, non negativo, al massimo 4 decimali (scala memorizzata di PurchaseCost).</summary>
    public decimal PurchaseCost { get; init; }
}

public sealed record PriceFormulaCalculationStepDto(string Label, decimal Percentage, decimal RunningValue);

/// <summary>Dettaglio del calcolo; non salva nulla: il prezzo si salva con l'aggiornamento di prodotto o variante.</summary>
public sealed record PriceFormulaCalculationDto(
    decimal PurchaseCost,
    IReadOnlyList<PriceFormulaCalculationStepDto> Steps,
    decimal UnroundedPrice,
    decimal RoundingStep,
    decimal FinalPrice);
