using FluentValidation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// API endpoints for inventory management and barcode scanning.
/// </summary>
public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/inventory")
            .WithOpenApi()
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
        MauwDbContext dbContext,
        IValidator<BarcodeScanRequest> validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return TypedResults.BadRequest(string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));
        }

        // Find the variant by barcode
        var variant = await dbContext.ProductVariants
            .FirstOrDefaultAsync(v => v.Barcode.ToLower() == request.Barcode.ToLower());

        if (variant == null)
        {
            return TypedResults.NotFound();
        }

        // Parse movement type
        if (request.Type < 0 || request.Type > 3)
        {
            return TypedResults.BadRequest("Invalid movement type");
        }
        var movementType = (StockMovementType)request.Type;

        // Create stock movement
        var movement = new StockMovement(
            variantId: variant.Id,
            type: movementType,
            quantity: request.Quantity,
            unitCost: variant.PurchaseCost,
            referenceDoc: request.ReferenceDoc
        );

        // Update variant stock atomically
        variant.UpdateStockQuantity(request.Quantity);

        dbContext.StockMovements.Add(movement);
        await dbContext.SaveChangesAsync();

        var dto = new StockMovementDto(
            Id: movement.Id,
            VariantId: movement.VariantId,
            Type: movement.Type.ToString(),
            Quantity: movement.Quantity,
            UnitCost: movement.UnitCost,
            ReferenceDoc: movement.ReferenceDoc,
            CreatedAt: movement.CreatedAt
        );

        return TypedResults.Ok(dto);
    }

    private static async Task<Ok<List<StockMovementDto>>> GetStockMovements(
        Guid variantId,
        MauwDbContext dbContext)
    {
        var movements = await dbContext.StockMovements
            .Where(m => m.VariantId == variantId)
            .OrderByDescending(m => m.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var dtos = movements.Select(m => new StockMovementDto(
            Id: m.Id,
            VariantId: m.VariantId,
            Type: m.Type.ToString(),
            Quantity: m.Quantity,
            UnitCost: m.UnitCost,
            ReferenceDoc: m.ReferenceDoc,
            CreatedAt: m.CreatedAt
        )).ToList();

        return TypedResults.Ok(dtos);
    }
}
