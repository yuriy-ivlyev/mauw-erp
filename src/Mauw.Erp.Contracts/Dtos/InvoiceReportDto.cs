namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Invoice PDF report generation.
/// </summary>
public record InvoiceReportDto(
    string InvoiceNumber,
    DateTime InvoiceDate,
    ContactReportDto Customer,
    ContactReportDto? Company,
    List<InvoiceReportLineDto> Lines,
    InvoiceTotalsDto Totals,
    string? LogoUrl = null
);