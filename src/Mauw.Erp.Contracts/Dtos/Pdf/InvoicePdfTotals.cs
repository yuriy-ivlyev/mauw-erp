namespace Mauw.Erp.Contracts.Dtos.Pdf;

/// <summary>Footer totals block for invoice PDF.</summary>
public sealed record InvoicePdfTotals(
    int     TotalPieces,
    decimal TotalNet,
    decimal TotalVat,
    decimal TotalGross,
    decimal ShippingCost = 0m
);
