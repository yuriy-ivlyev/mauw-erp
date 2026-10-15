namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for updating a product variant.
/// </summary>
public record UpdateProductVariantRequest(
    Guid? ColorId,
    Guid? SizeId,
    string Barcode,
    decimal PurchaseCost
);
