namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Contact information (response).
/// </summary>
public record ContactDto(
    Guid Id,
    int Type,
    string BusinessName,
    string VatNumber,
    string? SdiCode,
    decimal DefaultDiscountRate,
    decimal DefaultCommissionRate,
    Guid? AgentId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// DTO for stock movement (response).
/// </summary>
public record StockMovementDto(
    Guid Id,
    Guid VariantId,
    string Type,
    int Quantity,
    decimal UnitCost,
    string? ReferenceDoc,
    DateTime CreatedAt
);

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

/// <summary>
/// DTO for agent commission (response).
/// </summary>
public record AgentCommissionDto(
    Guid Id,
    Guid AgentId,
    Guid InvoiceId,
    decimal TaxableAmountBase,
    decimal CommissionRate,
    decimal CommissionAmount,
    string Status
);
