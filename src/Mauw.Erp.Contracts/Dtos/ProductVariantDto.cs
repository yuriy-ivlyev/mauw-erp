namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for ProductVariant information including pricing (response).
/// </summary>
public record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    string? ColorId,
    string? SizeId,
    string? Color,
    string? Size,
    string Barcode,
    decimal PurchaseCost,
    int StockQuantity,
    decimal ListPrice,
    decimal FinalNetPrice,
    decimal VatAmount,
    decimal FinalGrossPrice
);