namespace Mauw.Erp.Contracts.Dtos.Pdf;

/// <summary>Payment and banking data for invoice PDF.</summary>
public sealed record InvoicePdfPaymentInfo(
    /// <summary>E.g. "Bonifico 30/60 gg D.F.F.M."</summary>
    string   PaymentTerms,
    /// <summary>SDI code, e.g. "MP05"</summary>
    string   PaymentMethod,
    DateOnly? DueDate,
    string?  Iban,
    string?  Swift,
    string?  BankName
);
