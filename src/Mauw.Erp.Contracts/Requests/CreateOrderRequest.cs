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
}