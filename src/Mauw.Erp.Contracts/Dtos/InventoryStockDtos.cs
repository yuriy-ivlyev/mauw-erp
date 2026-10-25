using Mauw.Erp.Contracts.Enums;

namespace Mauw.Erp.Contracts.Dtos;

public sealed record InventoryStockDto(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductVariantId,
    string Sku,
    string Barcode,
    string Description,
    string? Color,
    string? Size,
    string? Category,
    decimal QuantityOnHand,
    decimal QuantityReserved,
    decimal QuantityIncoming,
    decimal QuantityAvailable,
    string UnitOfMeasure,
    [property: CostSensitive] decimal? UnitCost,
    [property: CostSensitive] decimal? TotalValue,
    [property: CostSensitive] string? CostSource);

public sealed record InventoryStockTotalsDto(
    decimal TotalPhysicalQuantity,
    decimal TotalReservedQuantity,
    decimal TotalIncomingQuantity,
    decimal TotalAvailableQuantity,
    [property: CostSensitive] decimal? TotalValue);

public sealed record InventoryStocksResponseDto(
    IReadOnlyList<InventoryStockDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    InventoryStockTotalsDto Totals,
    DateTime GeneratedAtUtc,
    DateTime? AtDate,
    string? AllocationNote);

public sealed record InventoryStockExportDto(
    IReadOnlyList<InventoryStockDto> Items,
    InventoryStockTotalsDto Totals,
    DateTime GeneratedAtUtc,
    DateTime? AtDate,
    string? AllocationNote,
    ValuationMethod ValuationMethod = ValuationMethod.WeightedAverageCost,
    InventoryStockFilterSummaryDto? Filters = null)
{
    /// <summary>False for exports redacted for roles without cost visibility: no cost/value columns or totals.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IncludesCosts => Totals.TotalValue.HasValue;
}

public sealed record InventoryStockMovementDto(
    Guid Id,
    DateTime MovementDateUtc,
    string MovementType,
    string? Reason,
    decimal Quantity,
    decimal? QuantityDelta,
    [property: CostSensitive] decimal? UnitCost,
    string? DocumentReference,
    string? Notes,
    decimal? RunningBalance);

public sealed record InventoryStockMovementsResponseDto(
    IReadOnlyList<InventoryStockMovementDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record AmbiguousInventoryMovementDto(
    Guid MovementId,
    Guid? WarehouseId,
    string? WarehouseCode,
    string? WarehouseName,
    Guid ProductVariantId,
    string Barcode,
    string ProductName,
    string MovementType,
    string? Reason,
    int Quantity,
    DateTime MovementDateUtc,
    string? DocumentReference,
    bool DirectionIsAmbiguous,
    decimal? DirectionKnownFromType,
    decimal? StockQuantityOnHand,
    decimal? KnownMovementsQuantity,
    decimal? UnexplainedQuantity,
    decimal? ProposedQuantityDelta,
    string ReconciliationStatus);

public sealed record InventoryMovementReconciliationResponseDto(
    IReadOnlyList<AmbiguousInventoryMovementDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    DateTime GeneratedAtUtc);

public sealed record InventoryStockFilterSummaryDto(
    string? Search,
    string? Warehouse,
    string? Category,
    bool OnlyInStock);
