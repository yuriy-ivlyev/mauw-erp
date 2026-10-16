namespace Mauw.Erp.Contracts.Requests;

public record CreateStockMovementCommand(
    Guid WarehouseId,
    Guid ProductVariantId,
    int Reason,
    decimal Quantity,
    string? Notes = null,
    string? DocumentReference = null,
    string? CreatedByUserId = null);

public record StockTransferCommand(
    Guid SourceWarehouseId,
    Guid TargetWarehouseId,
    Guid ProductVariantId,
    decimal Quantity,
    string? Notes = null);

public record StockFilterRequest(
    Guid? WarehouseId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 30);
