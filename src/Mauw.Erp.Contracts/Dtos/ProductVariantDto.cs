namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for ProductVariant information including pricing (response).
/// </summary>
public record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    string Size,
    string Color,
    string Barcode,
    decimal PurchaseCost,
    int StockQuantity,
    decimal ListPrice,
    decimal FinalNetPrice,
    decimal VatAmount,
    decimal FinalGrossPrice
);