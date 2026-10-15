using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Product service implementation.
/// </summary>
public class ProductService(IUnitOfWork unitOfWork, IValidator<CreateProductRequest> validator)
    : IProductService
{
    public async Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request)
    {
        var validationResult = validator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<ProductDto>.Fail(errors);
        }

        var product = new Product(
            supplierId: request.SupplierId,
            supplierCode: request.SupplierCode,
            internalCode: request.InternalCode,
            name: request.Name,
            defaultMarkupRate: request.DefaultMarkupRate,
            vatRate: request.VatRate,
            imageUrl: request.ImageUrl
        );

        foreach (var variantRequest in request.Variants)
        {
            var variant = new ProductVariant(
                productId: product.Id,
                size: variantRequest.Size,
                color: variantRequest.Color,
                barcode: variantRequest.Barcode,
                purchaseCost: variantRequest.PurchaseCost,
                stockQuantity: variantRequest.StockQuantity
            );
            product.Variants.Add(variant);
        }

        await unitOfWork.Products.AddAsync(product);
        await unitOfWork.SaveChangesAsync();

        return OperationResult<ProductDto>.Ok(ProductMapper.MapToDto(product));
    }

    public async Task<OperationResult<ProductDto>> GetProductAsync(Guid id)
    {
        var product = await unitOfWork.Products.GetByIdAsync(id);
        if (product == null)
            return OperationResult<ProductDto>.Fail("Product not found");

        return OperationResult<ProductDto>.Ok(ProductMapper.MapToDto(product));
    }

    public async Task<OperationResult<PagedResult<ProductDto>>> GetProductsAsync(int page, int pageSize, string? search = null)
    {
        var products = await unitOfWork.Products.GetPaginatedAsync(page, pageSize, search);
        var total = await unitOfWork.Products.GetTotalCountAsync(search);

        var dtos = ProductMapper.MapToListDto(products);
        var result = new PagedResult<ProductDto>(dtos, page, pageSize, total);

        return OperationResult<PagedResult<ProductDto>>.Ok(result);
    }

    public async Task<OperationResult<ProductDto>> UpdateProductAsync(Guid id, CreateProductRequest request)
    {
        var validationResult = validator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<ProductDto>.Fail(errors);
        }

        var product = await unitOfWork.Products.GetByIdAsync(id);
        if (product == null)
            return OperationResult<ProductDto>.Fail("Product not found");

        product.Update(
            supplierId: request.SupplierId,
            supplierCode: request.SupplierCode,
            internalCode: request.InternalCode,
            name: request.Name,
            defaultMarkupRate: request.DefaultMarkupRate,
            vatRate: request.VatRate,
            imageUrl: request.ImageUrl
        );

        // Update the product first
        await unitOfWork.Products.UpdateAsync(product);
        
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<ProductDto>.Fail(ex.Message);
        }

        // Delete old variants and save before inserting new ones to avoid unique constraint violations
        var oldVariants = product.Variants.ToList();
        foreach (var oldVariant in oldVariants)
        {
            await unitOfWork.ProductVariants.DeleteAsync(oldVariant);
        }

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<ProductDto>.Fail(ex.Message);
        }

        // Add new variants
        foreach (var variantRequest in request.Variants)
        {
            var variant = new ProductVariant(
                productId: product.Id,
                size: variantRequest.Size,
                color: variantRequest.Color,
                barcode: variantRequest.Barcode,
                purchaseCost: variantRequest.PurchaseCost,
                stockQuantity: variantRequest.StockQuantity
            );
            await unitOfWork.ProductVariants.AddAsync(variant);
        }

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<ProductDto>.Fail(ex.Message);
        }

        var updated = await unitOfWork.Products.GetByIdAsync(id);
        return OperationResult<ProductDto>.Ok(ProductMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<bool>> DeleteProductAsync(Guid id)
    {
        var product = await unitOfWork.Products.GetByIdAsync(id);
        if (product == null)
            return OperationResult<bool>.Fail("Product not found");

        await unitOfWork.Products.DeleteAsync(product);
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<bool>.Fail(ex.Message);
        }

        return OperationResult<bool>.Ok(true);
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