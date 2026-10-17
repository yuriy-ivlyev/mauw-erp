namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for updating a product variant.
/// </summary>
public record UpdateProductVariantRequest(
    string? ColorId,
    string? SizeId,
    string Barcode,
    decimal PurchaseCost,
    decimal? WeightGrams = null
);
