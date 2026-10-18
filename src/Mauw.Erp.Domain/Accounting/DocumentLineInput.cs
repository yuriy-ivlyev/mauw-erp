namespace Mauw.Erp.Domain.Accounting;

public sealed record DocumentLineInput(
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    string? NatureCode = null,
    decimal DiscountPercent = 0m,
    bool PriceIncludesVat = false);
