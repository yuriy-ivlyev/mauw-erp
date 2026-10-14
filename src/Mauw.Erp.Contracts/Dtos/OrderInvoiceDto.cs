namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Order information (response).
/// </summary>
public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid ContactId,
    DateTime OrderDate,
    decimal CustomerDiscountRate,
    string Status,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    List<OrderItemDto> Items
);

/// <summary>
/// DTO for OrderItem information (response).
/// </summary>
public record OrderItemDto(
    Guid Id,
    Guid OrderId,
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal LineNetTotal,
    decimal LineVatTotal,
    decimal LineGrossTotal
);

/// <summary>
/// DTO for Invoice information (response).
/// </summary>
public record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid ContactId,
    DateTime InvoiceDate,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    string Status,
    List<InvoiceItemDto> Items,
    List<PaymentDto> Payments
);

/// <summary>
/// DTO for InvoiceItem information (response).
/// </summary>
public record InvoiceItemDto(
    Guid Id,
    Guid InvoiceId,
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal LineNetTotal,
    decimal LineVatTotal,
    decimal LineGrossTotal
);

/// <summary>
/// DTO for Payment information (response).
/// </summary>
public record PaymentDto(
    Guid Id,
    decimal Amount,
    DateTime PaymentDate,
    string PaymentMethod
);

/// <summary>
/// DTO for DDT (Transport Document) information (response).
/// </summary>
public record DdtDto(
    Guid Id,
    string DdtNumber,
    Guid ContactId,
    DateTime DdtDate,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    List<DdtItemDto> Items
);

/// <summary>
/// DTO for DdtItem information (response).
/// </summary>
public record DdtItemDto(
    Guid Id,
    Guid DdtDocumentId,
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal LineNetTotal,
    decimal LineVatTotal,
    decimal LineGrossTotal
);
