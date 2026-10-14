using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
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
            .WithSummary("Create a new product with variants");

        group.MapGet("/", GetProducts)
            .WithName("GetProducts")
            .WithSummary("Get paginated product list");

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
}