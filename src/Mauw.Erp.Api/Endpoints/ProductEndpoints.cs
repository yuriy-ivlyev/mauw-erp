using FluentValidation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// API endpoints for product management and catalog operations.
/// </summary>
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/products")
            .WithOpenApi()
            .WithTags("Products");

        group.MapPost("/", CreateProduct)
            .WithName("CreateProduct")
            .WithSummary("Create a new product with variants");

        group.MapGet("/", GetProducts)
            .WithName("GetProducts")
            .WithSummary("Get paginated product list");

        group.MapGet("/{id}", GetProduct)
            .WithName("GetProduct")
            .WithSummary("Get product details by ID");

        group.MapGet("/barcode/{barcode}", GetVariantByBarcode)
            .WithName("GetVariantByBarcode")
            .WithSummary("Get variant by barcode for fast lookups");
    }

    private static async Task<Created<ProductDto>> CreateProduct(
        CreateProductRequest request,
        MauwDbContext dbContext,
        IValidator<CreateProductRequest> validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
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

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var dto = new ProductDto(
            Id: product.Id,
            SupplierId: product.SupplierId,
            SupplierCode: product.SupplierCode,
            InternalCode: product.InternalCode,
            Name: product.Name,
            DefaultMarkupRate: product.DefaultMarkupRate,
            VatRate: product.VatRate,
            ImageUrl: product.ImageUrl,
            Variants: product.Variants.Select(v => MapVariant(v, product.DefaultMarkupRate)).ToList()
        );

        return TypedResults.Created($"/api/v1/products/{product.Id}", dto);
    }

    private static async Task<Ok<Page<ProductDto>>> GetProducts(
        MauwDbContext dbContext,
        int page = 1,
        int pageSize = 20,
        string? search = null)
    {
        var query = dbContext.Products.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(search) ||
                                      p.InternalCode.ToLower().Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .Include(p => p.Variants)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        var dtos = items.Select(p => new ProductDto(
            Id: p.Id,
            SupplierId: p.SupplierId,
            SupplierCode: p.SupplierCode,
            InternalCode: p.InternalCode,
            Name: p.Name,
            DefaultMarkupRate: p.DefaultMarkupRate,
            VatRate: p.VatRate,
            ImageUrl: p.ImageUrl,
            Variants: p.Variants.Select(v => MapVariant(v, p.DefaultMarkupRate)).ToList()
        )).ToList();

        return TypedResults.Ok(new Page<ProductDto>(dtos, page, pageSize, total));
    }

    private static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
        Guid id,
        MauwDbContext dbContext)
    {
        var product = await dbContext.Products
            .Include(p => p.Variants)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return TypedResults.NotFound();
        }

        var dto = new ProductDto(
            Id: product.Id,
            SupplierId: product.SupplierId,
            SupplierCode: product.SupplierCode,
            InternalCode: product.InternalCode,
            Name: product.Name,
            DefaultMarkupRate: product.DefaultMarkupRate,
            VatRate: product.VatRate,
            ImageUrl: product.ImageUrl,
            Variants: product.Variants.Select(v => MapVariant(v, product.DefaultMarkupRate)).ToList()
        );

        return TypedResults.Ok(dto);
    }

    private static async Task<Results<Ok<BarcodeVariantDto>, NotFound>> GetVariantByBarcode(
        string barcode,
        MauwDbContext dbContext,
        PricingService pricingService)
    {
        var variant = await dbContext.ProductVariants
            .Include(v => v.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Barcode.ToLower() == barcode.ToLower());

        if (variant == null || variant.Product == null)
        {
            return TypedResults.NotFound();
        }

        // Calculate pricing
        var pricingResult = pricingService.CalculatePricing(
            variant.PurchaseCost,
            variant.Product.DefaultMarkupRate,
            0,
            variant.Product.VatRate
        );

        var dto = new BarcodeVariantDto(
            Barcode: variant.Barcode,
            VariantId: variant.Id,
            ProductId: variant.ProductId,
            ProductName: variant.Product.Name,
            Size: variant.Size,
            Color: variant.Color,
            StockQuantity: variant.StockQuantity,
            UnitNetPrice: pricingResult.FinalNetPrice,
            UnitGrossPrice: pricingResult.FinalGrossPrice,
            VatRate: variant.Product.VatRate
        );

        return TypedResults.Ok(dto);
    }

    private static ProductVariantDto MapVariant(ProductVariant variant, decimal defaultMarkupRate)
    {
        // Calculate pricing for the variant
        var pricingEngine = new PricingEngine();
        var pricingResult = pricingEngine.CalculatePrice(
            variant.PurchaseCost,
            defaultMarkupRate,
            0,
            0 // VAT will be set separately by product
        );

        return new ProductVariantDto(
            Id: variant.Id,
            ProductId: variant.ProductId,
            Size: variant.Size,
            Color: variant.Color,
            Barcode: variant.Barcode,
            PurchaseCost: variant.PurchaseCost,
            StockQuantity: variant.StockQuantity,
            ListPrice: pricingResult.ListPrice,
            FinalNetPrice: pricingResult.FinalNetPrice,
            VatAmount: 0, // Will be calculated by product VAT
            FinalGrossPrice: pricingResult.FinalNetPrice
        );
    }
}

/// <summary>
/// Generic page response DTO.
/// </summary>
public record Page<T>(List<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
