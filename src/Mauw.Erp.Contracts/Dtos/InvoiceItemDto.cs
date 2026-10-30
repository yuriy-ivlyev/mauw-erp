namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for InvoiceItem information (response).
/// </summary>
public record InvoiceItemDto(
    Guid Id,
    Guid InvoiceId,
    Guid VariantId,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal LineNetTotal,
    decimal? LineVatTotal,
    decimal? LineGrossTotal,
    string? NatureCode = null,
    decimal DiscountPercent = 0m,
    Mauw.Erp.Contracts.Pricing.PricingSnapshotDto? PricingSnapshot = null,
    string? Sku = null,
    string? ProductName = null,
    string? SizeName = null,
    string? ColorName = null
);