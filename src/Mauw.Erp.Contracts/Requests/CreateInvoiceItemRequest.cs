namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating an invoice item.
/// </summary>
public class CreateInvoiceItemRequest
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitNetPrice { get; set; }
    /// <summary>Legacy compatibility field; fiscal calculations use VatRate and NatureCode instead.</summary>
    public decimal UnitVatAmount { get; set; }
    public decimal? VatRate { get; set; }
    public string? NatureCode { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? UnitGrossPrice { get; set; }
    public Guid? PriceListId { get; set; }
    public string? OverrideReason { get; set; }
    public bool IsCustomerReturn { get; set; }
    public Guid? SourceLineId { get; set; }
    public Guid? SourceOrderItemId { get; set; }
}