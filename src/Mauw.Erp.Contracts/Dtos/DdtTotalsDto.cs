namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for DDT totals in reports.
/// </summary>
public record DdtTotalsDto(
    decimal TaxableAmount,
    List<VatBreakdownReportDto> VatBreakdown,
    decimal TotalVat,
    decimal TotalDue
);