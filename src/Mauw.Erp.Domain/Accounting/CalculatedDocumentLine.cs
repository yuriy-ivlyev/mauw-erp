namespace Mauw.Erp.Domain.Accounting;

public sealed record CalculatedDocumentLine(
    int LineNumber,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal DiscountPercent,
    decimal VatRate,
    string? NatureCode,
    decimal LineNetTotal);
