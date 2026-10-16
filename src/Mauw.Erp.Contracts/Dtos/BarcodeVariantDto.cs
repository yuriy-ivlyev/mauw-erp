namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for barcode lookup response.
/// </summary>
public record BarcodeVariantDto(
    string Barcode,
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string Size,
    string Color,
    decimal UnitNetPrice,
    decimal UnitGrossPrice,
    decimal VatRate,
    decimal QuantityAvailable = 0
);