namespace Mauw.Erp.Contracts.Dtos.Pdf;

/// <summary>A single line item in a fashion wholesale invoice PDF.</summary>
public sealed record InvoicePdfLineModel(
    string   Sku,
    string   Description,
    string?  SeasonCollection,
    int      Pieces,
    decimal  UnitPrice,
    decimal? DiscountPercent,
    decimal  VatRate,
    decimal  LineTotal
);
