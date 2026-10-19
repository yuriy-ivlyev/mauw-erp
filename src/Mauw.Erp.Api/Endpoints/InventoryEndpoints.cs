using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// API endpoints for inventory management and barcode scanning.
/// Thin layer delegating to IInventoryService.
/// </summary>
public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/inventory")
            .WithTags("Inventory");

        group.MapPost("/scan", ScanBarcode)
            .WithName("ScanBarcode")
            .WithSummary("Scan barcode and record stock movement");

        group.MapGet("/movements/{variantId}", GetStockMovements)
            .WithName("GetStockMovements")
            .WithSummary("Get stock movements for a variant");

        group.MapGet("/stock", GetStockSummary)
            .WithName("GetStockSummary")
            .WithSummary("Get paged stock summary");

        group.MapPost("/movements", ExecuteMovement)
            .WithName("ExecuteStockMovement")
            .WithSummary("Execute a stock movement");

        group.MapPost("/transfer", Transfer)
            .WithName("TransferStock")
            .WithSummary("Transfer stock between warehouses");

        group.MapGet("/stock/{warehouseId:guid}/{variantId:guid}", GetAvailableQty)
            .WithName("GetAvailableStockQuantity")
            .WithSummary("Get available quantity for a warehouse and variant");

        group.MapGet("/movements-paged", GetMovementsPaged)
            .WithName("GetMovementsPaged")
            .WithSummary("Get all stock movements paged");

        group.MapGet("/barcode/{barcode}", GetVariantByBarcodeWithStock)
            .WithName("GetVariantByBarcodeWithStock")
            .WithSummary("Get variant by barcode with warehouse stock");
    }

    private static async Task<Results<Ok<StockMovementDto>, BadRequest<string>, NotFound>> ScanBarcode(
        BarcodeScanRequest request,
        IInventoryService inventoryService)
    {
        var result = await inventoryService.ScanBarcodeAsync(request);
        
        if (!result.Success)
        {
            if (result.Error != null && result.Error.Contains("not found"))
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Ok<List<StockMovementDto>>> GetStockMovements(
        Guid variantId,
        IInventoryService inventoryService)
    {
        var result = await inventoryService.GetMovementsAsync(variantId, 1, 1000);
        return TypedResults.Ok(result.Data!.Items);
    }

    private static async Task<Ok<PagedResult<StockItemDto>>> GetStockSummary(
        IStockService stockService,
        Guid? warehouseId,
        string? search,
        int page = 1,
        int pageSize = 30)
    {
        var result = await stockService.GetStockSummaryPagedAsync(new StockFilterRequest(warehouseId, search, page, pageSize));
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<StockItemDto>, BadRequest<string>, NotFound>> ExecuteMovement(
        CreateStockMovementCommand command,
        IStockService stockService)
    {
        var result = await stockService.ExecuteMovementAsync(command);

        if (!result.Success)
        {
            if (result.Error is "Warehouse not found" or "Variant not found" or "Stock item not found")
                return TypedResults.NotFound();

            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<bool>, BadRequest<string>, NotFound>> Transfer(
        StockTransferCommand command,
        IStockService stockService)
    {
        var result = await stockService.TransferAsync(command);

        if (!result.Success)
        {
            if (result.Error is "Source warehouse not found" or "Target warehouse not found" or "Variant not found")
                return TypedResults.NotFound();

            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Ok<decimal>> GetAvailableQty(
        Guid warehouseId,
        Guid variantId,
        IStockService stockService)
    {
        var result = await stockService.GetAvailableQtyAsync(warehouseId, variantId);
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Ok<PagedResult<StockMovementDetailDto>>> GetMovementsPaged(
        IStockService stockService,
        Guid? warehouseId = null,
        int page = 1,
        int pageSize = 30)
    {
        var result = await stockService.GetMovementsPagedAsync(warehouseId, page, Math.Min(pageSize, 100));
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<BarcodeVariantDto>, BadRequest<string>, NotFound>> GetVariantByBarcodeWithStock(
        string barcode,
        Guid warehouseId,
        IInventoryService inventoryService)
    {
        var result = await inventoryService.GetVariantByBarcodeWithStockAsync(barcode, warehouseId);

        if (!result.Success)
            return result.Error == "Variant not found"
                ? TypedResults.NotFound()
                : TypedResults.BadRequest(result.Error);

        return TypedResults.Ok(result.Data!);
    }
}
