namespace Mauw.Erp.Contracts.Dtos;

public sealed record ShipmentDto(
    Guid Id,
    string ShipmentNumber,
    string DdtNumber,
    string? CarrierName,
    string? TrackingCode,
    int BoxCount,
    decimal? TotalWeightKg,
    DateTime ShippedAtUtc,
    IReadOnlyList<ShipmentLineDto> Lines);

public sealed record ShipmentLineDto(
    Guid OrderLineId,
    Guid ProductId,
    string Sku,
    int QuantityShipped);
