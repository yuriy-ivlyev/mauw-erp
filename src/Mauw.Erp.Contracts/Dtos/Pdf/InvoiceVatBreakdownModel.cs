namespace Mauw.Erp.Contracts.Dtos.Pdf;

/// <summary>Castelletto IVA — one row per VAT rate / exemption nature.</summary>
public sealed record InvoiceVatBreakdownModel(
    decimal  VatRate,
    decimal  TaxableAmount,
    decimal  VatAmount,
    /// <summary>SDI exemption nature code, e.g. "N2.1". Null for ordinary taxed rows.</summary>
    string?  ExemptionNature = null
);
