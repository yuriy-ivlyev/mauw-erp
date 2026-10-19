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
public class ProductService(IUnitOfWork unitOfWork, IValidator<CreateProductRequest> validator,
    CatalogPriceCalculator catalogPrices)
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
        if (!request.HasVariants)
        {
            var pricing = catalogPrices.Validate(request.SellingPrice);
            if (!pricing.Success)
                return OperationResult<ProductDto>.Fail(pricing.Error);
        }

        var product = new Product(
            supplierId: request.SupplierId,
            supplierCode: request.SupplierCode,
            internalCode: request.InternalCode,
            name: request.Name,
            defaultMarkupRate: 0m,
            vatRate: request.VatRate,
            basePurchaseCost: request.BasePurchaseCost,
            categoryId: request.CategoryId,
            collectionId: request.CollectionId,
            materialId: request.MaterialId,
            hasVariants: request.HasVariants
        ) { SellingPrice = request.HasVariants ? null : request.SellingPrice };

        await unitOfWork.Products.AddAsync(product);

        // For products without variants, auto-create a single default variant
        if (!product.HasVariants)
        {
            var defaultVariant = new ProductVariant(
                productId: product.Id,
                barcode: $"MNVR-{product.InternalCode.ToUpper()}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                purchaseCost: product.BasePurchaseCost,
                colorId: null,
                sizeId: null
            ) { SellingPrice = request.SellingPrice };
            await unitOfWork.ProductVariants.AddAsync(defaultVariant);
        }
        await unitOfWork.SaveChangesAsync();

        return ProductMapper.MapToDto(product);
    }

    public async Task<OperationResult<ProductDto>> GetProductAsync(Guid id)
    {
        var product = await unitOfWork.Products.GetByIdAsync(id);
        if (product == null)
            return OperationResult<ProductDto>.Fail("Product not found");

        return ProductMapper.MapToDto(product);
    }

    public async Task<OperationResult<PagedResult<ProductDto>>> GetProductsAsync(int page, int pageSize, string? search = null)
    {
        var products = await unitOfWork.Products.GetPaginatedAsync(page, pageSize, search);
        var total = await unitOfWork.Products.GetTotalCountAsync(search);

        var dtos = ProductMapper.MapToListDto(products);
        if (!dtos.Success)
            return OperationResult<PagedResult<ProductDto>>.Fail(dtos.Error!);
        var result = new PagedResult<ProductDto>(dtos.Data!, page, pageSize, total);

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

        if (product.HasVariants != request.HasVariants && product.Variants.Count > 0)
            return OperationResult<ProductDto>.Fail("Non e possibile cambiare tipologia a un prodotto con SKU gia presenti.");
        if (!request.HasVariants)
        {
            var pricing = catalogPrices.Validate(request.SellingPrice);
            if (!pricing.Success)
                return OperationResult<ProductDto>.Fail(pricing.Error);
        }
        product.Update(
            supplierId: request.SupplierId,
            supplierCode: request.SupplierCode,
            internalCode: request.InternalCode,
            name: request.Name,
            defaultMarkupRate: product.DefaultMarkupRate,
            vatRate: request.VatRate,
            basePurchaseCost: request.BasePurchaseCost,
            categoryId: request.CategoryId,
            collectionId: request.CollectionId,
            materialId: request.MaterialId,
            hasVariants: request.HasVariants
        );
        product.SellingPrice = request.HasVariants ? null : request.SellingPrice;
        if (!product.HasVariants && product.Variants.Count == 0)
        {
            var defaultVariant = new ProductVariant(product.Id,
                $"MNVR-{product.InternalCode.ToUpper()}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                request.BasePurchaseCost) { SellingPrice = request.SellingPrice };
            await unitOfWork.ProductVariants.AddAsync(defaultVariant);
        }
        if (!product.HasVariants)
            foreach (var variant in product.Variants)
            {
                variant.PurchaseCost = request.BasePurchaseCost;
                variant.SellingPrice = request.SellingPrice;
            }

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
        return ProductMapper.MapToDto(updated!);
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

        return ProductMapper.MapBarcodeVariantToDto(variant, variant.Product);
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
        if (!product.HasVariants)
            return OperationResult<ProductVariantDto>.Fail("Il prodotto semplice ha gia una SKU unica; non aggiungere varianti.");
        if (request.PurchaseCost < 0m)
            return OperationResult<ProductVariantDto>.Fail("Il costo di acquisto deve essere non negativo.");
        var pricing = catalogPrices.Validate(request.SellingPrice);
        if (!pricing.Success)
            return OperationResult<ProductVariantDto>.Fail(pricing.Error);

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
        ) { SellingPrice = request.SellingPrice };

        await unitOfWork.ProductVariants.AddAsync(variant);
        
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return OperationResult<ProductVariantDto>.Fail($"Failed to create variant: {ex.Message}");
        }

        return ProductMapper.MapVariantToDto(variant, product);
    }

    public async Task<OperationResult<List<ProductVariantDto>>> GetProductVariantsAsync(Guid productId)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product == null)
            return OperationResult<List<ProductVariantDto>>.Fail("Product not found");

        var variants = new List<ProductVariantDto>();
        foreach (var variant in product.Variants)
        {
            var result = ProductMapper.MapVariantToDto(variant, product);
            if (!result.Success)
                return OperationResult<List<ProductVariantDto>>.Fail(result.Error!);
            variants.Add(result.Data!);
        }
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
        if (request.PurchaseCost < 0m)
            return OperationResult<ProductVariantDto>.Fail("Il costo di acquisto deve essere non negativo.");
        var pricing = catalogPrices.Validate(request.SellingPrice);
        if (!pricing.Success)
            return OperationResult<ProductVariantDto>.Fail(pricing.Error);

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
        variant.SellingPrice = request.SellingPrice;
        if (!product.HasVariants)
        {
            product.SellingPrice = request.SellingPrice;
            product.BasePurchaseCost = request.PurchaseCost;
        }

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
        return ProductMapper.MapVariantToDto(updated!, product);
    }

    public async Task<OperationResult<bool>> DeleteProductVariantAsync(Guid productId, Guid variantId)
    {
        var product = await unitOfWork.Products.GetByIdAsync(productId);
        if (product == null)
            return OperationResult<bool>.Fail("Product not found");
        if (!product.HasVariants)
            return OperationResult<bool>.Fail("La SKU unica del prodotto semplice non puo essere eliminata separatamente.");

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
            var pricing = catalogPrices.GetStoredPrice(variant);
            if (!pricing.Success)
                return OperationResult<ProductMatrixDto>.Fail(pricing.Error);

            cells.Add(new MatrixCellDto(
                VariantId: variant.Id,
                ColorId: variant.ColorId,
                SizeId: variant.SizeId,
                Barcode: variant.Barcode,
                UnitNetPrice: pricing.Data!.Value,
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