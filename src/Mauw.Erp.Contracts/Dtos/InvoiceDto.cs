namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Invoice information (response).
/// </summary>
public record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid ContactId,
    Guid? OrderId,
    string ContactName,
    DateTime InvoiceDate,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    decimal ShippingCost,
    decimal? ShippingVatRate,
    string? ShippingNatureCode,
    DateTime? SdiSubmittedAt,
    decimal PaidAmount,
    decimal BalanceDue,
    string Status,
    List<InvoiceItemDto> Items,
    List<PaymentDto> Payments,
    string SdiStatus = "NotTransmitted",
    bool IsOrderUnderAdjustment = false
);