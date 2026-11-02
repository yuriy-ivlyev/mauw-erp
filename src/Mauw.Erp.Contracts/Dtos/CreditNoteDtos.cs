namespace Mauw.Erp.Contracts.Dtos;

public sealed record CreditNoteSummaryDto(
    Guid Id,
    string CreditNoteNumber,
    DateTime CreditNoteDate,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    string? Reason,
    string SdiStatus,
    decimal AllocatedAmount = 0.00m,
    decimal RemainingCredit = 0.00m);

public sealed record CreditNoteAvailableCreditDto(
    Guid Id,
    string CreditNoteNumber,
    DateTime CreditNoteDate,
    decimal GrossTotal,
    decimal AllocatedAmount,
    decimal RemainingCredit);

public sealed record CreditNoteSelectableLineDto(
    Guid? InvoiceItemId,
    bool IsShippingCharge,
    string Description,
    string? Sku,
    decimal OriginalQuantity,
    decimal CreditedQuantity,
    decimal RemainingQuantity,
    decimal UnitNetPrice,
    decimal DiscountPercent,
    decimal VatRate,
    string? NatureCode,
    decimal LineNetTotal);

public sealed record CreditNotePreviewLineDto(
    Guid? InvoiceItemId,
    bool IsShippingCharge,
    string Description,
    string? Sku,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal DiscountPercent,
    decimal VatRate,
    string? NatureCode,
    decimal LineNetTotal);

public sealed record CreditNoteVatSummaryDto(
    decimal VatRate,
    string? NatureCode,
    decimal TaxableAmount,
    decimal VatAmount);

public sealed record CreditNotePreviewDto(
    Guid InvoiceId,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    List<CreditNoteVatSummaryDto> VatSummary,
    List<CreditNoteSelectableLineDto> AvailableLines,
    List<CreditNotePreviewLineDto> SelectedLines);

public sealed record CreditNoteIssuedDto(
    CreditNoteSummaryDto CreditNote,
    byte[]? CourtesyPdf,
    string? PdfGenerationError);

public sealed record CreditNoteItemDto(
    Guid Id,
    Guid? SourceInvoiceItemId,
    Guid? VariantId,
    string Description,
    bool IsShippingCharge,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal DiscountPercent,
    decimal VatRate,
    string? NatureCode,
    decimal LineNetTotal,
    string? Sku,
    string? ProductName,
    string? SizeName,
    string? ColorName);

public sealed record CreditNoteDetailDto(
    Guid Id,
    string CreditNoteNumber,
    DateTime CreditNoteDate,
    Guid InvoiceId,
    string RelatedInvoiceNumber,
    DateTime RelatedInvoiceDate,
    Guid ContactId,
    string ContactName,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    string? Reason,
    bool ReturnToWarehouse,
    Guid? WarehouseId,
    string SdiStatus,
    string? SdiFileName,
    string? SdiTransmissionId,
    DateTime? SdiTransmittedAtUtc,
    List<CreditNoteItemDto> Items);

public sealed record FiscalDocumentTransmissionRequest(Guid Id, string DocumentType);

public sealed record BulkTransmitFiscalDocumentsRequest(
    IReadOnlyList<FiscalDocumentTransmissionRequest> Documents);

public sealed record FiscalDocumentTransmissionItemResultDto(
    Guid Id,
    string DocumentType,
    bool Success,
    string? DocumentNumber,
    string? ErrorMessage);

public sealed record BulkFiscalTransmissionResultDto(
    int ProcessedCount,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<FiscalDocumentTransmissionItemResultDto> Results);
