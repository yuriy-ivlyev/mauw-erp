namespace Mauw.Erp.Contracts.Requests;

public sealed record FinalizeCashSaleRequest(
    string PaymentMethod,
    string? TransactionDetails);

public sealed record ShipOrderRequest(
    string CarrierName,
    string? TrackingCode,
    List<ShipOrderItemRequest>? Items = null,
    int? BoxCount = null,
    decimal? WeightKg = null);

public sealed record ShipOrderItemRequest(Guid OrderItemId, int Quantity);

public sealed record CancelOrderRequest(string Reason);

public sealed record UpdateOrderShippingChargeRequest(
    decimal ShippingCost,
    decimal? VatRate,
    string? NatureCode);
