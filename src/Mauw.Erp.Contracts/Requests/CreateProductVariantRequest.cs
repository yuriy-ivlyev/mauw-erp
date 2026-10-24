namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a product variant.
/// </summary>
public record CreateProductVariantRequest(
    Guid ProductId,
    string? ColorId,
    string? SizeId,
    string Barcode,
    decimal PurchaseCost,
    decimal? WeightGrams = null,
    decimal? SellingPrice = null,
    string? SupplierCode = null
);