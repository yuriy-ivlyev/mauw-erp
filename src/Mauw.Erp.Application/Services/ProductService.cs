using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Product service implementation.
/// </summary>
public class ProductService(IUnitOfWork unitOfWork, IValidator<CreateProductRequest> validator)
    : IProductService
{
    private static readonly PricingEngine PricingEngine = new();
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
            basePurchaseCost: request.BasePurchaseCost,
            categoryId: request.CategoryId,
            collectionId: request.CollectionId,
            materialId: request.MaterialId,
            hasVariants: request.HasVariants
        );

        await unitOfWork.Products.AddAsync(product);

        // For products without variants, auto-create a single default variant
        if (!product.HasVariants)
        {
            var defaultBarcode = $"MNVR-{product.InternalCode}-{Guid.NewGuid():N[..8]}";
            var defaultVariant = new ProductVariant(
                productId: product.Id,
                barcode: $"MNVR-{product.InternalCode.ToUpper()}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                purchaseCost: product.BasePurchaseCost,
                colorId: null,
                sizeId: null
            );
            await unitOfWork.ProductVariants.AddAsync(defaultVariant);
        }
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
            basePurchaseCost: request.BasePurchaseCost,
            categoryId: request.CategoryId,
            collectionId: request.CollectionId,
            materialId: request.MaterialId,
            hasVariants: request.HasVariants
        );

        await unitOfWork.Products.UpdateAsync(product);

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

    public async Task<OperationResult<ProductImageDto>> AddImageAsync(Guid productId, AddProductImageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return OperationResult<ProductImageDto>.Fail("Image URL is required");

        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product is null)
            return OperationResult<ProductImageDto>.Fail("Product not found");

        // Check if this should be main image
        bool shouldBeMain = request.IsMain || product.Images.Count == 0;
        
        // If setting as main, demote all others
        if (shouldBeMain)
        {
            foreach (var img in product.Images)
                img.SetMain(false);
        }

        // Create new image (don't add to collection, add directly via repository)
        var displayOrder = product.Images.Count > 0 ? product.Images.Max(i => i.DisplayOrder) + 1 : 0;
        var newImage = new ProductImage(productId, request.Url, request.StorageKey, displayOrder, shouldBeMain, request.ProductVariantId);
        
        await unitOfWork.Products.AddImageAsync(newImage);

        try
        {
            await unitOfWork.SaveChangesAsync();
            return OperationResult<ProductImageDto>.Ok(ProductMapper.MapImageToDto(newImage));
        }
        catch (Exception ex)
        {
            return OperationResult<ProductImageDto>.Fail($"Failed to add image: {ex.Message}");
        }
    }

    public async Task<OperationResult<bool>> RemoveImageAsync(Guid productId, Guid imageId)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product is null)
            return OperationResult<bool>.Fail("Product not found");

        var image = product.Images.FirstOrDefault(i => i.Id == imageId);
        if (image is null)
            return OperationResult<bool>.Fail("Image not found");

        bool wasMain = image.IsMain;
        product.Images.Remove(image);

        // If removing main, promote first remaining
        if (wasMain && product.Images.Count > 0)
            product.Images.First().SetMain(true);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.Fail($"Failed to remove image: {ex.Message}");
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<bool>> SetMainImageAsync(Guid productId, Guid imageId)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product is null)
            return OperationResult<bool>.Fail("Product not found");

        var image = product.Images.FirstOrDefault(i => i.Id == imageId);
        if (image is null)
            return OperationResult<bool>.Fail("Image not found");

        // Demote all, then promote target
        foreach (var img in product.Images)
            img.SetMain(false);
        
        image.SetMain(true);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.Fail($"Failed to set main image: {ex.Message}");
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<ProductVariantDto>> CreateProductVariantAsync(
        Guid productId,
        CreateProductVariantRequest request)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product == null)
            return OperationResult<ProductVariantDto>.Fail("Product not found");

        if (!string.IsNullOrWhiteSpace(request.ColorId))
        {
            var color = await unitOfWork.Colors.GetByIdAsync(request.ColorId);
            if (color == null)
                return OperationResult<ProductVariantDto>.Fail("Color not found");
        }

        if (!string.IsNullOrWhiteSpace(request.SizeId))
        {
            var size = await unitOfWork.Sizes.GetByIdAsync(request.SizeId);
            if (size == null)
                return OperationResult<ProductVariantDto>.Fail("Size not found");
        }

        var variant = new ProductVariant(
            productId: productId,
            colorId: request.ColorId,
            sizeId: request.SizeId,
            barcode: request.Barcode,
            purchaseCost: request.PurchaseCost,
            weightGrams: request.WeightGrams
        );

        await unitOfWork.ProductVariants.AddAsync(variant);
        
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return OperationResult<ProductVariantDto>.Fail($"Failed to create variant: {ex.Message}");
        }

        return OperationResult<ProductVariantDto>.Ok(ProductMapper.MapVariantToDto(variant, product.DefaultMarkupRate));
    }

    public async Task<OperationResult<List<ProductVariantDto>>> GetProductVariantsAsync(Guid productId)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product == null)
            return OperationResult<List<ProductVariantDto>>.Fail("Product not found");

        var variants = product.Variants.Select(v => ProductMapper.MapVariantToDto(v, product.DefaultMarkupRate)).ToList();
        return OperationResult<List<ProductVariantDto>>.Ok(variants);
    }

    public async Task<OperationResult<ProductVariantDto>> UpdateProductVariantAsync(
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product == null)
            return OperationResult<ProductVariantDto>.Fail("Product not found");

        var variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
        if (variant == null)
            return OperationResult<ProductVariantDto>.Fail("Variant not found");

        // Validate Color if provided
        if (!string.IsNullOrWhiteSpace(request.ColorId))
        {
            var color = await unitOfWork.Colors.GetByIdAsync(request.ColorId);
            if (color == null)
                return OperationResult<ProductVariantDto>.Fail("Color not found");
        }

        // Validate Size if provided
        if (!string.IsNullOrWhiteSpace(request.SizeId))
        {
            var size = await unitOfWork.Sizes.GetByIdAsync(request.SizeId);
            if (size == null)
                return OperationResult<ProductVariantDto>.Fail("Size not found");
        }

        variant.Update(
            colorId: request.ColorId,
            sizeId: request.SizeId,
            barcode: request.Barcode,
            purchaseCost: request.PurchaseCost,
            weightGrams: request.WeightGrams
        );

        await unitOfWork.ProductVariants.UpdateAsync(variant);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return OperationResult<ProductVariantDto>.Fail($"Failed to update variant: {ex.Message}");
        }

        var updated = await unitOfWork.ProductVariants.GetByIdAsync(variantId);
        return OperationResult<ProductVariantDto>.Ok(ProductMapper.MapVariantToDto(updated!, product.DefaultMarkupRate));
    }

    public async Task<OperationResult<bool>> DeleteProductVariantAsync(Guid productId, Guid variantId)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product == null)
            return OperationResult<bool>.Fail("Product not found");

        var variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
        if (variant == null)
            return OperationResult<bool>.Fail("Variant not found");

        await unitOfWork.ProductVariants.DeleteAsync(variant);
        
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.Fail($"Failed to delete variant: {ex.Message}");
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<List<ProductParentDto>>> SearchParentProductsAsync(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return OperationResult<List<ProductParentDto>>.Ok([]);

        var products = await unitOfWork.Products.SearchParentsAsync(query.Trim(), Math.Min(limit, 50));
        var dtos = products.Select(p => new ProductParentDto(
            ProductId: p.Id,
            SupplierCode: p.SupplierCode,
            InternalCode: p.InternalCode,
            Name: p.Name,
            HasVariants: p.HasVariants,
            VariantCount: p.Variants.Count
        )).ToList();

        return OperationResult<List<ProductParentDto>>.Ok(dtos);
    }

    public async Task<OperationResult<ProductMatrixDto>> GetProductMatrixAsync(Guid productId, Guid warehouseId)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product == null)
            return OperationResult<ProductMatrixDto>.Fail("Product not found");

        var sizes = product.Variants
            .Where(v => v.Size != null)
            .Select(v => v.Size!)
            .DistinctBy(s => s.Id)
            .OrderBy(s => s.Name)
            .Select((s, idx) => new MatrixSizeDto(s.Id, s.Name, idx))
            .ToList();

        var colors = product.Variants
            .Where(v => v.Color != null)
            .Select(v => v.Color!)
            .DistinctBy(c => c.Id)
            .OrderBy(c => c.Name)
            .Select(c => new MatrixColorDto(c.Id, c.Name, c.HtmlColor))
            .ToList();

        var cells = new List<MatrixCellDto>();
        foreach (var variant in product.Variants)
        {
            var stock = await unitOfWork.Stock.GetAvailableQtyAsync(warehouseId, variant.Id);
            var pricingResult = PricingEngine.CalculatePrice(
                variant.PurchaseCost, product.DefaultMarkupRate, 0, product.VatRate);

            cells.Add(new MatrixCellDto(
                VariantId: variant.Id,
                ColorId: variant.ColorId,
                SizeId: variant.SizeId,
                Barcode: variant.Barcode,
                UnitNetPrice: pricingResult.FinalNetPrice,
                VatRate: product.VatRate,
                AvailableStock: stock,
                WeightGrams: variant.WeightGrams
            ));
        }

        var matrix = new ProductMatrixDto(
            ProductId: product.Id,
            SupplierCode: product.SupplierCode,
            ProductName: product.Name,
            HasVariants: product.HasVariants,
            Sizes: sizes,
            Colors: colors,
            Cells: cells
        );

        return OperationResult<ProductMatrixDto>.Ok(matrix);
    }
}