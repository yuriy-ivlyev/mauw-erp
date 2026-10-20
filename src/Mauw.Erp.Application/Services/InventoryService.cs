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

        // Stock management moved to separate system
        // TODO: Integrate with new inventory tracking

        await unitOfWork.StockMovements.AddAsync(movement);
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
        var (movements, total) = await unitOfWork.StockMovements.GetByVariantIdPagedAsync(variantId, page, pageSize);
        var dtos = movements.Select(m => new StockMovementDto(
            Id: m.Id,
            VariantId: m.VariantId,
            Type: m.Type.ToString(),
            Quantity: m.Quantity,
            UnitCost: m.UnitCost,
            ReferenceDoc: m.ReferenceDoc,
            CreatedAt: m.CreatedAt
        )).ToList();

        var result = new PagedResult<StockMovementDto>(dtos.ToList(), page, pageSize, total);
        return OperationResult<PagedResult<StockMovementDto>>.Ok(result);
    }

    public async Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode)
    {
        var variant = await unitOfWork.ProductVariants.GetByBarcodeAsync(barcode);
        if (variant?.Product == null)
            return OperationResult<BarcodeVariantDto>.Fail("Variant not found");

        return ProductMapper.MapBarcodeVariantToDto(variant, variant.Product);
    }

    public async Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeWithStockAsync(string barcode, Guid warehouseId)
    {
        var variant = await unitOfWork.ProductVariants.GetByBarcodeAsync(barcode);
        if (variant?.Product == null)
            return OperationResult<BarcodeVariantDto>.Fail("Variant not found");

        var quantityAvailable = await unitOfWork.Stock.GetAvailableQtyAsync(warehouseId, variant.Id);
        var pricing = ProductMapper.MapBarcodeVariantToDto(variant, variant.Product);
        if (!pricing.Success)
            return pricing;
        var dto = pricing.Data! with
        {
            QuantityAvailable = quantityAvailable
        };

        return OperationResult<BarcodeVariantDto>.Ok(dto);
    }
}