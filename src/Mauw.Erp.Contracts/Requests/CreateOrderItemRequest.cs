namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating an order item.
/// </summary>
public class CreateOrderItemRequest
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
}