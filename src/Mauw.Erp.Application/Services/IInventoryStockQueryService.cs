using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public sealed record InventoryStocksPageDto(
    IReadOnlyList<InventoryStockDto> Items,
    int Page,
    int PageSize,
    DateTime GeneratedAtUtc,
    DateTime? AtDate,
    string? AllocationNote);

public sealed record InventoryStocksSummaryDto(
    int TotalCount,
    int PageSize,
    int TotalPages,
    InventoryStockTotalsDto Totals);

public interface IInventoryStockQueryService
{
    Task<OperationResult<InventoryStocksPageDto>> GetStocksPageAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult<InventoryStocksResponseDto>> GetStocksAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult<InventoryStocksSummaryDto>> GetStocksSummaryAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult<InventoryStockExportDto>> GetExportAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult<InventoryStockMovementsResponseDto>> GetMovementsAsync(
        Guid warehouseId,
        Guid productVariantId,
        DateTime? atDate,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<OperationResult<InventoryMovementReconciliationResponseDto>> GetAmbiguousMovementsAsync(
        Guid? warehouseId = null,
        Guid? productVariantId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
