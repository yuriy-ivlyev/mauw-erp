namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new product.
/// </summary>
public record CreateProductRequest(
    Guid SupplierId,
    string SupplierCode,
    string Barcode,
    string Name,
    decimal? SellingPrice,
    decimal VatRate,
    decimal BasePurchaseCost,
    Guid? CategoryId = null,
    Guid? CollectionId = null,
    Guid? MaterialId = null,
    bool HasVariants = true
);