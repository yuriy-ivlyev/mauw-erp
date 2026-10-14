namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for a line item in SDI import preview.
/// </summary>
public record SdiImportLineDto(
    int LineNumber,
    string? CodiceArticolo,
    string CodiceTipo,
    string Descrizione,
    decimal Quantita,
    decimal PrezzoUnitario,
    decimal AliquotaIVA
);