namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for updating a product.
/// </summary>
public record UpdateProductRequest(
    Guid SupplierId,
    string SupplierCode,
    string InternalCode,
    string Name,
    decimal DefaultMarkupRate,
    decimal VatRate,
    decimal BasePurchaseCost,
    Guid? CategoryId = null,
    Guid? CollectionId = null,
    Guid? MaterialId = null
);
