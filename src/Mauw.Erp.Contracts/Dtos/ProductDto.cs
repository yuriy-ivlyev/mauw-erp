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