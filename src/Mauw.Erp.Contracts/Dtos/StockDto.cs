namespace Mauw.Erp.Contracts.Dtos;

public record StockItemDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductVariantId,
    string Barcode,
    string ProductName,
    string? Color,
    string? Size,
    decimal QuantityOnHand,
    decimal QuantityReserved,
    decimal QuantityAvailable,
    decimal? UnitCost,
    decimal? EstimatedValue);

public record StockMovementDetailDto(
    Guid Id,
    Guid VariantId,
    string Barcode,
    string ProductName,
    Guid? WarehouseId,
    string? WarehouseCode,
    string? ReasonLabel,
    decimal Quantity,
    DateTime MovementDateUtc,
    string? Notes,
    string? DocumentReference);
