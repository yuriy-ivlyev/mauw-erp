namespace Mauw.Erp.Domain.Accounting;

/// <summary>Uno sconto di riga: esattamente uno tra percentuale e importo in euro sul totale di riga.</summary>
public sealed record DiscountInput(decimal? Percent, decimal? Amount);

/// <summary>Percentuale effettiva unica e importi risparmiati, questi ultimi solo per la presentazione.</summary>
public sealed record CombinedDiscount(decimal EffectivePercent, IReadOnlyList<decimal> SavedAmounts);
