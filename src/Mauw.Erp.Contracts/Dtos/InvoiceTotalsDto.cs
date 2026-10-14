namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for invoice totals in reports.
/// </summary>
public record InvoiceTotalsDto(
    decimal TaxableAmount,
    List<VatBreakdownReportDto> VatBreakdown,
    decimal TotalVat,
    decimal TotalDue
);