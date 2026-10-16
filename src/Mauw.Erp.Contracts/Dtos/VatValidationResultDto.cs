namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// Risultato della validazione P.IVA / Tax ID con eventuale verifica VIES.
/// </summary>
public record VatValidationResultDto(
    bool IsFormatValid,
    /// <summary>Null se paese non UE o VIES non consultato.</summary>
    bool? IsViesValid,
    bool IsViesAvailable,
    /// <summary>Ragione sociale restituita da VIES (solo UE).</summary>
    string? CompanyName,
    /// <summary>Indirizzo grezzo restituito da VIES (solo UE).</summary>
    string? CompanyAddress,
    string? ErrorMessage
);
