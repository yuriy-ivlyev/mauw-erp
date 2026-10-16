using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// API endpoints for product management and catalog operations.
/// Thin layer that delegates to IProductService.
/// </summary>
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/products")
            .WithTags("Products");

        group.MapPost("/", CreateProduct)
            .WithName("CreateProduct")
            .WithSummary("Create a new product");

        group.MapGet("/", GetProducts)
            .WithName("GetProducts")
            .WithSummary("Get paginated product list");

        group.MapGet("/search-parents", SearchParentProducts)
            .WithName("SearchParentProducts")
            .WithSummary("Cerca prodotti padre per nome o codice fornitore (per Cassa/POS)");

        group.MapGet("/{productId}/matrix", GetProductMatrix)
            .WithName("GetProductMatrix")
            .WithSummary("Ottieni la matrice taglia/colore con giacenze per un prodotto");

        group.MapGet("/variants/search", SearchVariants)
            .WithName("SearchVariants")
            .WithSummary("Ricerca varianti per barcode, nome prodotto, colore o taglia (autocomplete)");

        group.MapGet("/barcode/{barcode}", GetVariantByBarcode)
            .WithName("GetVariantByBarcode")
            .WithSummary("Get variant by barcode for fast lookups");

        group.MapGet("/{id}", GetProduct)
            .WithName("GetProduct")
            .WithSummary("Get product details by ID");

        group.MapPut("/{id}", UpdateProduct)
            .WithName("UpdateProduct")
            .WithSummary("Update product details");

        group.MapDelete("/{id}", DeleteProduct)
            .WithName("DeleteProduct")
            .WithSummary("Delete a product and its variants");

        // Product variant endpoints
        group.MapPost("/{productId}/variants", CreateProductVariant)
            .WithName("CreateProductVariant")
            .WithSummary("Create a new variant for a product");

        group.MapGet("/{productId}/variants", GetProductVariants)
            .WithName("GetProductVariants")
            .WithSummary("Get all variants for a product");

        group.MapPatch("/{productId}/variants/{variantId}", UpdateProductVariant)
            .WithName("UpdateProductVariant")
            .WithSummary("Update a product variant");

        group.MapDelete("/{productId}/variants/{variantId}", DeleteProductVariant)
            .WithName("DeleteProductVariant")
            .WithSummary("Delete a product variant");

        // Image gallery endpoints
        group.MapPost("/{id}/images", AddImage)
            .WithName("AddProductImage")
            .WithSummary("Add an image to the product gallery");

        group.MapDelete("/{id}/images/{imageId}", RemoveImage)
            .WithName("RemoveProductImage")
            .WithSummary("Remove an image from the product gallery");

        group.MapPut("/{id}/images/{imageId}/main", SetMainImage)
            .WithName("SetMainProductImage")
            .WithSummary("Set an image as the main product image");
    }

    private static async Task<Results<Created<ProductDto>, BadRequest<string>>> CreateProduct(
        CreateProductRequest request,
        IProductService productService)
    {
        var result = await productService.CreateProductAsync(request);
        
        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        return TypedResults.Created($"/api/v1/products/{result.Data!.Id}", result.Data);
    }

    private static async Task<Ok<PagedResult<ProductDto>>> GetProducts(
        IProductService productService,
        int page = 1,
        int pageSize = 20,
        string? search = null)
    {
        var result = await productService.GetProductsAsync(page, pageSize, search);
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
        Guid id,
        IProductService productService)
    {
        var result = await productService.GetProductAsync(id);
        
        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Ok<List<VariantSearchDto>>> SearchVariants(
        string? q,
        IUnitOfWork unitOfWork,
        IStockService stockService,
        Guid? warehouseId = null,
        int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 1)
            return TypedResults.Ok(new List<VariantSearchDto>());

        var variants = await unitOfWork.ProductVariants.SearchAsync(q.Trim(), Math.Min(limit, 30));

        var results = new List<VariantSearchDto>();
        foreach (var v in variants)
        {
            var stock = warehouseId.HasValue
                ? (await stockService.GetAvailableQtyAsync(warehouseId.Value, v.Id)).Data
                : 0m;

            results.Add(new VariantSearchDto(
                VariantId:    v.Id,
                ProductId:    v.ProductId,
                ProductName:  v.Product.Name,
                Sku:          v.Barcode, // barcode doubles as SKU in this domain
                Barcode:      v.Barcode,
                ColorName:    v.Color?.Name,
                SizeName:     v.Size?.Name,
                CurrentStock: stock));
        }

        return TypedResults.Ok(results);
    }

    private static async Task<Results<Ok<BarcodeVariantDto>, NotFound>> GetVariantByBarcode(
        string barcode,
        IProductService productService)
    {
        var result = await productService.GetVariantByBarcodeAsync(barcode);
        
        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<ProductDto>, BadRequest<string>, NotFound>> UpdateProduct(
        Guid id,
        CreateProductRequest request,
        IProductService productService)
    {
        var result = await productService.UpdateProductAsync(id, request);
        
        if (!result.Success)
        {
            if (result.Error == "Product not found")
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteProduct(
        Guid id,
        IProductService productService)
    {
        var result = await productService.DeleteProductAsync(id);
        
        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }

    private static async Task<Results<Created<ProductImageDto>, BadRequest<string>, NotFound>> AddImage(
        Guid id,
        AddProductImageRequest request,
        IProductService productService)
    {
        var result = await productService.AddImageAsync(id, request);

        if (!result.Success)
        {
            if (result.Error == "Product not found")
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Created($"/api/v1/products/{id}/images/{result.Data!.Id}", result.Data);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> RemoveImage(
        Guid id,
        Guid imageId,
        IProductService productService)
    {
        var result = await productService.RemoveImageAsync(id, imageId);

        if (!result.Success)
        {
            if (result.Error == "Product not found" || result.Error == "Image not found")
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> SetMainImage(
        Guid id,
        Guid imageId,
        IProductService productService)
    {
        var result = await productService.SetMainImageAsync(id, imageId);

        if (!result.Success)
        {
            if (result.Error == "Product not found" || result.Error == "Image not found")
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Created<ProductVariantDto>, BadRequest<string>, NotFound>> CreateProductVariant(
        Guid productId,
        CreateProductVariantRequest request,
        IProductService productService)
    {
        var result = await productService.CreateProductVariantAsync(productId, request);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error ?? "Failed to create variant");
        }

        return TypedResults.Created(
            $"/api/v1/products/{productId}/variants/{result.Data!.Id}",
            result.Data);
    }

    private static async Task<Results<Ok<List<ProductVariantDto>>, NotFound>> GetProductVariants(
        Guid productId,
        IProductService productService)
    {
        var result = await productService.GetProductVariantsAsync(productId);

        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<ProductVariantDto>, BadRequest<string>, NotFound>> UpdateProductVariant(
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request,
        IProductService productService)
    {
        var result = await productService.UpdateProductVariantAsync(productId, variantId, request);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error ?? "Failed to update variant");
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> DeleteProductVariant(
        Guid productId,
        Guid variantId,
        IProductService productService)
    {
        var result = await productService.DeleteProductVariantAsync(productId, variantId);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error ?? "Failed to delete variant");
        }

        return TypedResults.NoContent();
    }

    private static async Task<Ok<List<ProductParentDto>>> SearchParentProducts(
        IProductService productService,
        string? q = null,
        int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return TypedResults.Ok(new List<ProductParentDto>());

        var result = await productService.SearchParentProductsAsync(q, limit);
        return TypedResults.Ok(result.Data ?? []);
    }

    private static async Task<Results<Ok<ProductMatrixDto>, NotFound>> GetProductMatrix(
        Guid productId,
        IProductService productService,
        Guid? warehouseId = null)
    {
        var wh = warehouseId ?? Guid.Empty;
        var result = await productService.GetProductMatrixAsync(productId, wh);

        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Data!);
    }
}