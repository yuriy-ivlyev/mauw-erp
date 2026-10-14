namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for invoice line item in reports.
/// </summary>
public record InvoiceReportLineDto(
    string Codice,
    string Descrizione,
    string? TagliaColore,
    int Quantita,
    decimal PrezzoUnitario,
    decimal Totale
);