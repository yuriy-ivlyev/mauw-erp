using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Api.Endpoints;

public static class WarehouseEndpoints
{
    public static void MapWarehouseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/warehouses")
            .WithName("Warehouses");

        group.MapGet("/", ListWarehouses)
            .WithName("ListWarehouses")
            .WithSummary("Get paginated warehouses");

        group.MapGet("/{id:guid}", GetWarehouse)
            .WithName("GetWarehouse")
            .WithSummary("Get warehouse detail by ID");

        group.MapPost("/", CreateWarehouse)
            .WithName("CreateWarehouse")
            .WithSummary("Create a new warehouse");

        group.MapPut("/{id:guid}", UpdateWarehouse)
            .WithName("UpdateWarehouse")
            .WithSummary("Update an existing warehouse");

        group.MapDelete("/{id:guid}", DeleteWarehouse)
            .WithName("DeleteWarehouse")
            .WithSummary("Soft-delete a warehouse");

        group.MapPatch("/{id:guid}/set-default", SetDefaultWarehouse)
            .WithName("SetDefaultWarehouse")
            .WithSummary("Set a warehouse as default");
    }

    private static async Task<Results<Ok<PagedResult<WarehouseSummaryDto>>, BadRequest<string>>> ListWarehouses(
        IWarehouseService warehouseService,
        ILogger<Program> logger,
        string? search = null,
        int? type = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20)
    {
        var result = await warehouseService.GetPagedAsync(page, Math.Min(pageSize, 100), search, type, isActive);
        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        logger.LogInformation("Listed warehouses - total: {Total}", result.Data?.TotalCount);
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<WarehouseDetailDto>, NotFound>> GetWarehouse(
        Guid id,
        IWarehouseService warehouseService)
    {
        var result = await warehouseService.GetByIdAsync(id);
        return result.Success
            ? TypedResults.Ok(result.Data!)
            : TypedResults.NotFound();
    }

    private static async Task<Results<Created<WarehouseDetailDto>, BadRequest<string>>> CreateWarehouse(
        CreateWarehouseRequest request,
        IWarehouseService warehouseService,
        ILogger<Program> logger)
    {
        var result = await warehouseService.CreateAsync(request);
        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        logger.LogInformation("Warehouse created: {Id}", result.Data!.Id);
        return TypedResults.Created($"/api/v1/warehouses/{result.Data.Id}", result.Data);
    }

    private static async Task<Results<Ok<WarehouseDetailDto>, BadRequest<string>, NotFound>> UpdateWarehouse(
        Guid id,
        UpdateWarehouseRequest request,
        IWarehouseService warehouseService)
    {
        var result = await warehouseService.UpdateAsync(id, request);
        if (!result.Success)
        {
            if (result.Error == "Warehouse not found") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> DeleteWarehouse(
        Guid id,
        IWarehouseService warehouseService)
    {
        var result = await warehouseService.DeleteAsync(id);
        if (!result.Success)
        {
            if (result.Error == "Warehouse not found") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<WarehouseDetailDto>, BadRequest<string>, NotFound>> SetDefaultWarehouse(
        Guid id,
        IWarehouseService warehouseService)
    {
        var result = await warehouseService.SetDefaultAsync(id);
        if (!result.Success)
        {
            if (result.Error == "Warehouse not found") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }
}
