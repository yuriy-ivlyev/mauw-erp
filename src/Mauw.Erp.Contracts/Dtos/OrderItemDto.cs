namespace Mauw.Erp.Contracts.Dtos;

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
    decimal? LineVatTotal,
    decimal? LineGrossTotal,
    string? NatureCode = null,
    decimal DiscountPercent = 0m,
    Mauw.Erp.Contracts.Pricing.PricingSnapshotDto? PricingSnapshot = null
);