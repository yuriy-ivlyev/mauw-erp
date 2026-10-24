namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// Lightweight product parent DTO for POS search results (no variant detail).
/// </summary>
public record ProductParentDto(
    Guid ProductId,
    string SupplierCode,
    string Barcode,
    string Name,
    bool HasVariants,
    int VariantCount
);
