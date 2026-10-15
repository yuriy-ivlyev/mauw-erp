namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for a single product image.
/// </summary>
public record ProductImageDto(
    Guid Id,
    Guid ProductId,
    string Url,
    string? StorageKey,
    int DisplayOrder,
    bool IsMain,
    Guid? ProductVariantId
);
