using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Services;

public class StockService(
    IUnitOfWork unitOfWork,
    ILogger<StockService> logger) : IStockService
{
    public async Task<OperationResult<StockItemDto>> ExecuteMovementAsync(CreateStockMovementCommand command)
    {
        if (!Enum.IsDefined(typeof(StockMovementReason), command.Reason))
            return OperationResult<StockItemDto>.Fail("Invalid stock movement reason");

        if (command.Quantity == 0)
            return OperationResult<StockItemDto>.Fail("Quantity must be different from zero");

        var warehouse = await unitOfWork.Warehouses.GetByIdAsync(command.WarehouseId);
        if (warehouse is null)
            return OperationResult<StockItemDto>.Fail("Warehouse not found");

        var variant = await unitOfWork.ProductVariants.GetByIdAsync(command.ProductVariantId);
        if (variant is null)
            return OperationResult<StockItemDto>.Fail("Variant not found");

        var movement = new StockMovement(
            command.ProductVariantId,
            command.WarehouseId,
            (StockMovementReason)command.Reason,
            command.Quantity,
            command.Notes,
            command.CreatedByUserId,
            command.DocumentReference);

        try
        {
            await unitOfWork.Stock.RecordMovementAsync(movement, command.Quantity);
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<StockItemDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to execute stock movement for warehouse {WarehouseId} and variant {VariantId}",
                command.WarehouseId,
                command.ProductVariantId);
            return OperationResult<StockItemDto>.Fail("Unable to execute stock movement");
        }

        var stockItem = await unitOfWork.Stock.GetStockItemAsync(command.WarehouseId, command.ProductVariantId);
        if (stockItem is null)
            return OperationResult<StockItemDto>.Fail("Stock item not found");

        return OperationResult<StockItemDto>.Ok(Map(stockItem));
    }

    public async Task<OperationResult<PagedResult<StockItemDto>>> GetStockSummaryPagedAsync(StockFilterRequest request)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 30 : Math.Min(request.PageSize, 100);

        var (items, total) = await unitOfWork.Stock.GetStockByWarehousePagedAsync(
            request.WarehouseId,
            request.Search,
            page,
            pageSize,
            request.SortBy,
            request.SortDescending);

        var dtos = items.Select(Map).ToList();
        return OperationResult<PagedResult<StockItemDto>>.Ok(new PagedResult<StockItemDto>(dtos, page, pageSize, total));
    }

    public async Task<OperationResult<bool>> TransferAsync(StockTransferCommand command)
    {
        if (command.SourceWarehouseId == command.TargetWarehouseId)
            return OperationResult<bool>.Fail("Source and target warehouses must be different");

        if (command.Quantity <= 0)
            return OperationResult<bool>.Fail("Quantity must be greater than zero");

        var sourceWarehouse = await unitOfWork.Warehouses.GetByIdAsync(command.SourceWarehouseId);
        if (sourceWarehouse is null)
            return OperationResult<bool>.Fail("Source warehouse not found");

        var targetWarehouse = await unitOfWork.Warehouses.GetByIdAsync(command.TargetWarehouseId);
        if (targetWarehouse is null)
            return OperationResult<bool>.Fail("Target warehouse not found");

        var variant = await unitOfWork.ProductVariants.GetByIdAsync(command.ProductVariantId);
        if (variant is null)
            return OperationResult<bool>.Fail("Variant not found");

        var available = await unitOfWork.Stock.GetAvailableQtyAsync(command.SourceWarehouseId, command.ProductVariantId);
        if (available < command.Quantity)
            return OperationResult<bool>.Fail("Insufficient available stock");

        try
        {
            await unitOfWork.BeginTransactionAsync();

            await unitOfWork.Stock.RecordMovementAsync(
                new StockMovement(
                    command.ProductVariantId,
                    command.SourceWarehouseId,
                    StockMovementReason.InternalTransfer,
                    -command.Quantity,
                    command.Notes,
                    null,
                    null),
                -command.Quantity);

            await unitOfWork.Stock.RecordMovementAsync(
                new StockMovement(
                    command.ProductVariantId,
                    command.TargetWarehouseId,
                    StockMovementReason.InternalTransfer,
                    command.Quantity,
                    command.Notes,
                    null,
                    null),
                command.Quantity);

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();

            return OperationResult<bool>.Ok(true);
        }
        catch (ConcurrencyException ex)
        {
            await unitOfWork.RollbackAsync();
            return OperationResult<bool>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync();
            logger.LogWarning(
                ex,
                "Failed to transfer stock for variant {VariantId} from {SourceWarehouseId} to {TargetWarehouseId}",
                command.ProductVariantId,
                command.SourceWarehouseId,
                command.TargetWarehouseId);
            return OperationResult<bool>.Fail("Unable to transfer stock");
        }
    }

    public async Task<OperationResult<decimal>> GetAvailableQtyAsync(Guid warehouseId, Guid variantId)
    {
        var qty = await unitOfWork.Stock.GetAvailableQtyAsync(warehouseId, variantId);
        return OperationResult<decimal>.Ok(qty);
    }

    public async Task<OperationResult<PagedResult<StockMovementDetailDto>>> GetMovementsPagedAsync(
        Guid? warehouseId, int page, int pageSize, string? sortBy = null, bool sortDescending = false)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 30 : pageSize;
        pageSize = Math.Min(pageSize, 100);

        var (items, total) = await unitOfWork.Stock.GetMovementsPagedAsync(warehouseId, page, pageSize, sortBy, sortDescending);
        var dtos = items.Select(MapMovement).ToList();
        return OperationResult<PagedResult<StockMovementDetailDto>>.Ok(
            new PagedResult<StockMovementDetailDto>(dtos, page, pageSize, total));
    }

    public static string? GetReasonLabel(StockMovementReason? reason) => reason switch
    {
        StockMovementReason.SupplierReceipt     => "Carico Fornitore",
        StockMovementReason.ShowroomSale        => "Vendita Cassa",
        StockMovementReason.OrderShipment       => "Spedizione Ordine",
        StockMovementReason.InternalTransfer    => "Trasferimento",
        StockMovementReason.CustomerReturn      => "Reso Cliente",
        StockMovementReason.SupplierReturn      => "Reso Fornitore",
        StockMovementReason.InventoryAdjustment => "Rettifica Inventario",
        _                                       => null
    };

    private static StockMovementDetailDto MapMovement(Domain.Entities.StockMovement m) => new(
        m.Id,
        m.VariantId,
        m.Variant?.Barcode ?? "",
        m.Variant?.Product?.Name ?? "",
        m.WarehouseId,
        m.Warehouse?.Code,
        GetReasonLabel(m.Reason),
        m.Quantity,
        m.MovementDateUtc,
        m.Notes,
        m.ReferenceDoc);

    private static StockItemDto Map(StockItem s) => new(
        s.Id,
        s.WarehouseId,
        s.Warehouse.Code,
        s.Warehouse.Name,
        s.ProductVariantId,
        s.ProductVariant.Barcode,
        s.ProductVariant.Product.Name,
        s.ProductVariant.Color?.Name,
        s.ProductVariant.Size?.Name,
        s.QuantityOnHand,
        s.QuantityReserved,
        s.QuantityAvailable,
        s.UnitCost,
        s.UnitCost.HasValue ? s.QuantityOnHand * s.UnitCost.Value : null);
}
