namespace Mauw.Erp.Contracts.Requests;

public sealed record BulkTransmitInvoicesToSdiRequest(List<Guid> InvoiceIds);
