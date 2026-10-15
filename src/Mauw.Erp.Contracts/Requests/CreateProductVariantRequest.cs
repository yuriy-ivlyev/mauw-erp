namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a product variant.
/// </summary>
public record CreateProductVariantRequest(
    Guid ProductId,
    Guid ColorId,
    Guid SizeId,
    string Barcode,
    decimal PurchaseCost
);