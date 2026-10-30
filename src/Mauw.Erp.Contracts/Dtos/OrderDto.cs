using Mauw.Erp.Contracts.Enums;

namespace Mauw.Erp.Contracts.Dtos;

public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid ContactId,
    string ContactName,
    DateTime OrderDate,
    decimal CustomerDiscountRate,
    OrderStatusEnum Status,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    string OrderType,
    string FulfillmentType,
    List<OrderItemDto> Items,
    OrderLinkedDocumentsDto? LinkedDocuments,
    string StatusDescription,
    bool IsReadOnly,
    List<OrderPermittedActionDto> PermittedActions,
    bool CanEditInvoice = false,
    string? InternalNotes = null,
    decimal ShippingCost = 0.00m,
    decimal? ShippingVatRate = null,
    string? ShippingNatureCode = null
);

public record OrderPermittedActionDto(
    string Action,
    string Label,
    string Endpoint,
    string Method,
    string Style,
    bool RequiresDialog,
    string? DialogType
);

public record OrderLinkedDocumentsDto(
    Guid? InvoiceId,
    string? InvoiceNumber,
    Guid? DdtId,
    string? DdtNumber,
    List<CreditNoteRefDto> CreditNotes,
    List<DdtDocumentRefDto>? Ddts = null
);

public record DdtDocumentRefDto(Guid Id, string DdtNumber, DateTime DdtDate);

public record CreditNoteRefDto(
    Guid Id,
    string CreditNoteNumber,
    DateTime CreditNoteDate,
    decimal GrossTotal,
    string? Reason
);
