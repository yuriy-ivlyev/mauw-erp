namespace Mauw.Erp.Contracts.Dtos.Pdf;

/// <summary>Optional agent / representative info for invoice PDF.</summary>
public sealed record InvoicePdfAgentInfo(
    string  Name,
    string? Code
);
