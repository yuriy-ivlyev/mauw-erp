namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating an invoice.
/// </summary>
public class CreateInvoiceRequest
{
    public Guid ContactId { get; set; }
}