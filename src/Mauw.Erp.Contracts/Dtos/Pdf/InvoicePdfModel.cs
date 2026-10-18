namespace Mauw.Erp.Contracts.Dtos.Pdf;

/// <summary>
/// Root model for generating a fashion wholesale invoice PDF.
/// All MigraDoc types are strictly confined to the Infrastructure layer;
/// this record contains only primitive and domain-level types.
/// </summary>
public sealed record InvoicePdfModel(
    string                       DocumentNumber,
    /// <summary>SDI document type, e.g. "TD01".</summary>
    string                       DocumentType,
    DateOnly                     IssueDate,
    /// <summary>ISO 4217 currency code, e.g. "EUR".</summary>
    string                       Currency,
    InvoicePdfSellerDetails      Seller,
    InvoicePdfCustomerDetails    Customer,
    InvoicePdfPaymentInfo        Payment,
    List<InvoicePdfLineModel>    Lines,
    List<InvoiceVatBreakdownModel> VatSummary,
    InvoicePdfTotals             Totals,
    InvoicePdfAgentInfo?         Agent             = null,
    string?                      CustomerOrderRef  = null,
    DateOnly?                    CustomerOrderDate = null,
    string?                      DdtRef            = null,
    string?                      DeliveryTerms     = null,
    string?                      FiscalNotes       = null
);
