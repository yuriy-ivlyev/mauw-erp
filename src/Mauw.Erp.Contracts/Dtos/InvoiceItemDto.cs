namespace Mauw.Erp.Contracts.Dtos;

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