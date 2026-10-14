namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Order information (response).
/// </summary>
public record OrderDto(
    Guid Id,
    Guid ContactId,
    string OrderNumber,
    string Status,
    DateTime OrderDate,
    decimal CustomerDiscountRate,
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
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal LineNetTotal,
    decimal VatRate,
    decimal LineVatTotal,
    decimal LineGrossTotal
);

/// <summary>
/// DTO for Invoice information (response).
/// </summary>
public record InvoiceDto(
    Guid Id,
    Guid ContactId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    decimal PaidAmount,
    List<InvoiceItemDto> Items
);

/// <summary>
/// DTO for InvoiceItem information (response).
/// </summary>
public record InvoiceItemDto(
    Guid Id,
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal LineNetTotal,
    decimal VatRate,
    decimal LineVatTotal,
    decimal LineGrossTotal
);

/// <summary>
/// DTO for VAT breakdown (Castelletto IVA).
/// </summary>
public record VatBreakdownDto(
    decimal VatRate,
    decimal TaxableAmount,
    decimal VatAmount
);
