namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for VAT breakdown in reports.
/// </summary>
public record VatBreakdownReportDto(
    decimal VatRate,
    decimal TaxableAmount,
    decimal VatAmount
);