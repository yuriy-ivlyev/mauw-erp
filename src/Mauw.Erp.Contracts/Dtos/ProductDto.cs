namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Product information (response).
/// </summary>
public record ProductDto(
    Guid Id,
    Guid SupplierId,
    string SupplierCode,
    string InternalCode,
    string Name,
    decimal DefaultMarkupRate,
    decimal VatRate,
    string? ImageUrl,
    List<ProductVariantDto> Variants
);

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

/// <summary>
/// DTO for barcode lookup response.
/// </summary>
public record BarcodeVariantDto(
    string Barcode,
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string Size,
    string Color,
    int StockQuantity,
    decimal UnitNetPrice,
    decimal UnitGrossPrice,
    decimal VatRate
);
