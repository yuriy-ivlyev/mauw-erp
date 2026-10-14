using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Inventory service implementation.
/// </summary>
public class InventoryService(
    IUnitOfWork unitOfWork,
    IValidator<BarcodeScanRequest> barcodeValidator,
    PricingEngine pricingEngine)
    : IInventoryService
{
    private readonly PricingEngine _pricingEngine = pricingEngine;

    public async Task<OperationResult<StockMovementDto>> ScanBarcodeAsync(BarcodeScanRequest request)
    {
        var validationResult = barcodeValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<StockMovementDto>.Fail(errors);
        }

        var variant = await unitOfWork.ProductVariants.GetByBarcodeAsync(request.Barcode);
        if (variant == null)
            return OperationResult<StockMovementDto>.Fail("Barcode not found");

        // Create stock movement
        var movementType = (StockMovementType)request.Type;
        var movement = new StockMovement(
            variantId: variant.Id,
            type: movementType,
            quantity: request.Quantity,
            unitCost: variant.PurchaseCost,
            referenceDoc: request.ReferenceDoc ?? ""
        );

        // Update variant stock
        variant.UpdateStockQuantity(request.Quantity);

        await unitOfWork.StockMovements.AddAsync(movement);
        await unitOfWork.ProductVariants.UpdateAsync(variant);
        await unitOfWork.SaveChangesAsync();

        var dto = new StockMovementDto(
            Id: movement.Id,
            VariantId: movement.VariantId,
            Type: movement.Type.ToString(),
            Quantity: movement.Quantity,
            UnitCost: movement.UnitCost,
            ReferenceDoc: movement.ReferenceDoc,
            CreatedAt: movement.CreatedAt
        );

        return OperationResult<StockMovementDto>.Ok(dto);
    }

    public async Task<OperationResult<PagedResult<StockMovementDto>>> GetMovementsAsync(Guid variantId, int page, int pageSize)
    {
        var movements = await unitOfWork.StockMovements.GetByVariantIdAsync(variantId);
        var paginated = movements
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var total = movements.Count();
        var dtos = paginated.Select(m => new StockMovementDto(
            Id: m.Id,
            VariantId: m.VariantId,
            Type: m.Type.ToString(),
            Quantity: m.Quantity,
            UnitCost: m.UnitCost,
            ReferenceDoc: m.ReferenceDoc,
            CreatedAt: m.CreatedAt
        )).ToList();

        var result = new PagedResult<StockMovementDto>(dtos, page, pageSize, total);
        return OperationResult<PagedResult<StockMovementDto>>.Ok(result);
    }

    public async Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode)
    {
        var variant = await unitOfWork.ProductVariants.GetByBarcodeAsync(barcode);
        if (variant?.Product == null)
            return OperationResult<BarcodeVariantDto>.Fail("Variant not found");

        var dto = ProductMapper.MapBarcodeVariantToDto(variant, variant.Product);
        return OperationResult<BarcodeVariantDto>.Ok(dto);
    }
}