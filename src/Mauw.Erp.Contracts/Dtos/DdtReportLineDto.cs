namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for DDT line item in reports.
/// </summary>
public record DdtReportLineDto(
    string Codice,
    string Descrizione,
    string? TagliaColore,
    int Quantita,
    decimal PrezzoUnitario,
    decimal Totale
);