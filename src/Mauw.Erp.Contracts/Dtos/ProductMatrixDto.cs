namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// Size entry in the product matrix (ordered by display position).
/// </summary>
public record MatrixSizeDto(string Id, string Name, int DisplayOrder);

/// <summary>
/// Color entry in the product matrix.
/// </summary>
public record MatrixColorDto(string Id, string Name, string HtmlColor);

/// <summary>
/// A single cell in the taglia×colore matrix.
/// </summary>
public record MatrixCellDto(
    Guid VariantId,
    string? ColorId,
    string? SizeId,
    string Barcode,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal AvailableStock,
    decimal? WeightGrams = null
);

/// <summary>
/// Full product matrix DTO for POS taglia/colore selection.
/// </summary>
public record ProductMatrixDto(
    Guid ProductId,
    string SupplierCode,
    string ProductName,
    bool HasVariants,
    List<MatrixSizeDto> Sizes,
    List<MatrixColorDto> Colors,
    List<MatrixCellDto> Cells
);
