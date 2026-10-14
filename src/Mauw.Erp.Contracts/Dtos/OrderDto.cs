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