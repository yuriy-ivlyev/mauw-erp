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
    decimal? SellingPrice,
    decimal VatRate,
    decimal BasePurchaseCost,
    List<ProductImageDto> Images,
    List<ProductVariantDto> Variants,
    bool HasVariants = true,
    Guid? CategoryId = null,
    string? CategoryName = null,
    Guid? CollectionId = null,
    string? CollectionName = null,
    Guid? MaterialId = null,
    string? MaterialName = null
)
{
    /// <summary>Convenience accessor for the main image URL, for backward-compatible use in list views.</summary>
    public string? MainImageUrl => Images.FirstOrDefault(i => i.IsMain)?.Url ?? Images.FirstOrDefault()?.Url;
};