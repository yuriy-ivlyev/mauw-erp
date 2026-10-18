namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating an order item.
/// </summary>
public class CreateOrderItemRequest
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public string? NatureCode { get; set; }
    public decimal? UnitNetPrice { get; set; }
    public decimal? UnitGrossPrice { get; set; }
    public decimal? VatRate { get; set; }
    public decimal? DiscountPercent { get; set; }
}