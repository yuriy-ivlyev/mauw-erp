namespace Mauw.Erp.Contracts.Dtos;

public record CassaCartItemDto(
    Guid VariantId,
    string Barcode,
    string ProductId,
    string ProductName,
    string Size,
    string Color,
    decimal UnitNetPrice,
    decimal UnitGrossPrice,
    decimal VatRate,
    int Quantity,
    int MaxQuantity,
    decimal? WeightGrams
);

public record CassaSessionDetailDto(
    Guid Id,
    string SessionCode,
    string? ContactId,
    string? ContactName,
    string? ContactVatNumber,
    string? DocumentReference,
    List<CassaCartItemDto> Items
);

public record CassaSessionSummaryDto(
    Guid Id,
    string SessionCode,
    string? ContactName,
    int CartItemCount,
    DateTime UpdatedAt
);
