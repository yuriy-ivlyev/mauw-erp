namespace Mauw.Erp.Contracts.Dtos;

public record CassaCartItemDto(
    Guid VariantId,
    string Barcode,
    string ProductId,
    string ProductName,
    string Size,
    string Color,
    decimal UnitNetPrice,
    decimal VatRate,
    int Quantity,
    int MaxQuantity,
    decimal? WeightGrams,
    string? NatureCode = null,
    decimal DiscountPercent = 0m
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

public record CassaPreviewLineDto(
    int LineNumber,
    decimal UnitNetPrice,
    decimal DiscountPercent,
    decimal VatRate,
    string? NatureCode,
    decimal LineNetTotal
);

public record CassaPreviewVatDto(
    decimal VatRate,
    string? NatureCode,
    decimal TaxableAmount,
    decimal VatAmount
);

/// <summary>
/// Fiscal result computed by ItalianAccountingCalculator; the POS only displays it.
/// </summary>
public record CassaPreviewDto(
    List<CassaPreviewLineDto> Lines,
    List<CassaPreviewVatDto> VatSummary,
    decimal TotalTaxable,
    decimal TotalVat,
    decimal TotalGross
);
