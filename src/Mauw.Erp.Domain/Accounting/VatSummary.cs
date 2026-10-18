namespace Mauw.Erp.Domain.Accounting;

public sealed record VatSummary(
    decimal VatRate,
    string? NatureCode,
    decimal TaxableAmount,
    decimal VatAmount);
