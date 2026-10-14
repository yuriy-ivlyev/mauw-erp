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
    }

    private static async Task<Results<Ok<StockMovementDto>, BadRequest<string>, NotFound>> ScanBarcode(
        BarcodeScanRequest request,
        IInventoryService inventoryService)
    {
        var result = await inventoryService.ScanBarcodeAsync(request);
        
        if (!result.Success)
        {
            if (result.Error.Contains("not found"))
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
}
