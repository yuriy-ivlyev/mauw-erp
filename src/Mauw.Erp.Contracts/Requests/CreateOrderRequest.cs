namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating an order.
/// </summary>
public class CreateOrderRequest
{
    public Guid ContactId { get; set; }
    public decimal? DiscountRate { get; set; }
    /// <summary>Standard | Preventivo | WebOrder (default: Standard)</summary>
    public string? OrderType { get; set; }
    /// <summary>StandardShipping | CashDesk (default: StandardShipping)</summary>
    public string? FulfillmentType { get; set; }
    public string? InternalNotes { get; set; }
    public decimal? ShippingCost { get; set; }
    public decimal? ShippingVatRate { get; set; }
    public string? ShippingNatureCode { get; set; }
}