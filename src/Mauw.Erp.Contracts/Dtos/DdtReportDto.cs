namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for DDT (Transport Document) PDF report generation.
/// </summary>
public record DdtReportDto(
    string DdtNumber,
    DateTime DdtDate,
    ContactReportDto Customer,
    ContactReportDto? Company,
    List<DdtReportLineDto> Lines,
    DdtTotalsDto Totals,
    string? LogoUrl = null
);