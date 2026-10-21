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

public class StockFilterRequest : PageSortRequest
{
    public StockFilterRequest(
        Guid? warehouseId = null,
        string? search = null,
        int page = 1,
        int pageSize = 30,
        string? sortBy = null,
        bool sortDescending = false)
    {
        WarehouseId = warehouseId;
        Search = search;
        Page = page;
        PageSize = pageSize;
        SortBy = sortBy;
        SortDescending = sortDescending;
    }

    public Guid? WarehouseId { get; init; }
    public string? Search { get; init; }
}
