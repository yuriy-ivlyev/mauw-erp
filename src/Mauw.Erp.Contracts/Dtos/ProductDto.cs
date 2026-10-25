namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Product information (response).
/// </summary>
public record ProductDto(
    Guid Id,
    Guid SupplierId,
    string SupplierCode,
    string Barcode,
    string Name,
    decimal? SellingPrice,
    decimal VatRate,
    [property: CostSensitive] decimal? BasePurchaseCost,
    List<ProductVariantDto> Variants,
    bool HasVariants = true,
    Guid? CategoryId = null,
    string? CategoryName = null,
    Guid? CollectionId = null,
    string? CollectionName = null,
    Guid? MaterialId = null,
    string? MaterialName = null,
    /// <summary>List thumbnail: cover image of the product, or of its varianteVetrina.</summary>
    string? MainImageUrl = null
);