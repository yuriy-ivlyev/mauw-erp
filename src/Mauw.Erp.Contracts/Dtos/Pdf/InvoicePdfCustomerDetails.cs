namespace Mauw.Erp.Contracts.Dtos.Pdf;

/// <summary>Cessionario / Committente (customer) data for invoice PDF.</summary>
public sealed record InvoicePdfCustomerDetails(
    string  BusinessName,
    string? VatNumber,
    string? FiscalCode,
    string  Address,
    string  City,
    string  PostalCode,
    string  Province,
    string? SdiCode,
    string? Pec,
    /// <summary>ISO 3166-1 alpha-2 country of the customer; "IT" when not specified.</summary>
    string  CountryCode = "IT"
);
