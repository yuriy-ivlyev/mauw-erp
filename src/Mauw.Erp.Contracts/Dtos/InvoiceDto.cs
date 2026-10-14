namespace Mauw.Erp.Contracts.Dtos;

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