namespace Mauw.Erp.Contracts.Dtos;

/// <summary>DTO leggero per la ricerca autocomplete di varianti prodotto.</summary>
public record VariantSearchDto(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string? Sku,
    string Barcode,
    string? ColorName,
    string? SizeName,
    decimal CurrentStock,  // giacenza disponibile nel magazzino richiesto (0 se non specificato)
    string? SupplierCode,
    [property: CostSensitive] decimal? PurchaseCost
);
