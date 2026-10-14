namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for SDI XML import preview.
/// </summary>
public record SdiImportPreviewDto(
    string SupplierBusinessName,
    string SupplierVatNumber,
    string InvoiceNumber,
    DateTime InvoiceDate,
    List<SdiImportLineDto> Lines
);