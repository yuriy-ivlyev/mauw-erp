namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating an invoice item.
/// </summary>
public class CreateInvoiceItemRequest
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal UnitVatAmount { get; set; }
}