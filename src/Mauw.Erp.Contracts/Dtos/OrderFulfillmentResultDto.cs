namespace Mauw.Erp.Contracts.Dtos;

public sealed record OrderFulfillmentResultDto(
    OrderDto Order,
    string DocumentFileName,
    string ContentType,
    byte[] DocumentBytes,
    ShipmentDto? Shipment = null);
