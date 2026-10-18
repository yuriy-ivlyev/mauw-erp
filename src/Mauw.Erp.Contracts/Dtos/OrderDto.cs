namespace Mauw.Erp.Contracts.Dtos;

public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid ContactId,
    string ContactName,
    DateTime OrderDate,
    decimal CustomerDiscountRate,
    string Status,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    string OrderType,
    List<OrderItemDto> Items,
    OrderLinkedDocumentsDto? LinkedDocuments
);

public record OrderLinkedDocumentsDto(
    Guid? InvoiceId,
    string? InvoiceNumber,
    Guid? DdtId,
    string? DdtNumber,
    List<CreditNoteRefDto> CreditNotes
);

public record CreditNoteRefDto(
    Guid Id,
    string CreditNoteNumber,
    DateTime CreditNoteDate,
    decimal GrossTotal,
    string? Reason
);
