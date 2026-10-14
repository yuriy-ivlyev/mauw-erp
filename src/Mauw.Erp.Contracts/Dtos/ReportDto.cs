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

/// <summary>
/// DTO for contact information in reports.
/// </summary>
public record ContactReportDto(
    string BusinessName,
    string VatNumber,
    string? SdiCode,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string? Province = null,
    string? Country = null
);

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

/// <summary>
/// DTO for invoice totals in reports.
/// </summary>
public record InvoiceTotalsDto(
    decimal TaxableAmount,
    List<VatBreakdownReportDto> VatBreakdown,
    decimal TotalVat,
    decimal TotalDue
);

/// <summary>
/// DTO for DDT totals in reports.
/// </summary>
public record DdtTotalsDto(
    decimal TaxableAmount,
    List<VatBreakdownReportDto> VatBreakdown,
    decimal TotalVat,
    decimal TotalDue
);

/// <summary>
/// DTO for VAT breakdown in reports.
/// </summary>
public record VatBreakdownReportDto(
    decimal VatRate,
    decimal TaxableAmount,
    decimal VatAmount
);
