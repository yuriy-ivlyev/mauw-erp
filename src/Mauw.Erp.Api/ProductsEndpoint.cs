using FluentValidation;
using Mauw.Erp.Contracts;
using Mauw.Erp.Domain;
using Mauw.Erp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Api;

public static class ProductsEndpoint
{
    public static void MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/products", async (CreateProductRequest request, MauwDbContext db, IValidator<CreateProductRequest> validator) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var existingProduct = await db.Products
                .FirstOrDefaultAsync(p => p.SupplierId == request.SupplierId && p.SupplierCode == request.SupplierCode);

            if (existingProduct is not null)
            {
                return Results.Conflict(new { message = "A product with the same supplier and supplier code already exists." });
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                SupplierId = request.SupplierId,
                SupplierCode = request.SupplierCode.Trim(),
                InternalCode = request.InternalCode.Trim(),
                Name = request.Name.Trim(),
                DefaultMarkupRate = request.DefaultMarkupRate,
                VatRate = request.VatRate,
                ImageUrl = request.ImageUrl?.Trim()
            };

            foreach (var variantRequest in request.Variants)
            {
                product.AddVariant(new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    Size = variantRequest.Size.Trim(),
                    Color = variantRequest.Color.Trim(),
                    Barcode = variantRequest.Barcode.Trim(),
                    PurchaseCost = variantRequest.PurchaseCost,
                    StockQuantity = variantRequest.StockQuantity
                });
            }

            db.Products.Add(product);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/products/{product.Id}", MapToDto(product));
        })
        .WithName("CreateProduct")
        .ProducesValidationProblem()
        .Produces<ProductDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/products", async (MauwDbContext db, string? search = null, int page = 1, int pageSize = 20) =>
        {
            var query = db.Products
                .AsNoTracking()
                .Include(p => p.Variants)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.Trim();
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, $"%{normalized}%") ||
                    EF.Functions.ILike(p.SupplierCode, $"%{normalized}%"));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => MapToDto(p))
                .ToListAsync();

            return Results.Ok(new
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = items
            });
        })
        .WithName("ListProducts")
        .Produces<object>(StatusCodes.Status200OK);

        app.MapGet("/api/v1/variants/barcode/{barcode}", async (string barcode, MauwDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return Results.BadRequest(new { message = "Barcode is required." });
            }

            var variant = await db.ProductVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Barcode == barcode.Trim());

            if (variant is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new ProductVariantDto
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                Size = variant.Size,
                Color = variant.Color,
                Barcode = variant.Barcode,
                PurchaseCost = variant.PurchaseCost,
                StockQuantity = variant.StockQuantity
            });
        })
        .WithName("GetVariantByBarcode")
        .Produces<ProductVariantDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest);
    }

    private static ProductDto MapToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            SupplierId = product.SupplierId,
            SupplierCode = product.SupplierCode,
            InternalCode = product.InternalCode,
            Name = product.Name,
            DefaultMarkupRate = product.DefaultMarkupRate,
            VatRate = product.VatRate,
            ImageUrl = product.ImageUrl,
            Variants = product.Variants
                .Select(v => new ProductVariantDto
                {
                    Id = v.Id,
                    ProductId = v.ProductId,
                    Size = v.Size,
                    Color = v.Color,
                    Barcode = v.Barcode,
                    PurchaseCost = v.PurchaseCost,
                    StockQuantity = v.StockQuantity
                })
                .ToList()
        };
    }
}
