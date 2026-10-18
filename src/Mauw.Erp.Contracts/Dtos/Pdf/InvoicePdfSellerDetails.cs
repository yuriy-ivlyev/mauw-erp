namespace Mauw.Erp.Contracts.Dtos.Pdf;

/// <summary>Cedente / Prestatore (seller) data for invoice PDF.</summary>
public sealed record InvoicePdfSellerDetails(
    string  BusinessName,
    string  VatNumber,
    string? FiscalCode,
    string  Address,
    string  City,
    string  PostalCode,
    string  Province,
    string? Rea,
    string? ShareCapital,
    /// <summary>E.g. "RF01 - Ordinario"</summary>
    string  TaxRegime,
    string? SdiCode,
    string? Pec
);
