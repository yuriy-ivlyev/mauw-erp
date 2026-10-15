using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Decorators;

public sealed class TracingProductService(IProductService inner) : TracingDecoratorBase, IProductService
{
    public Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request) =>
        TraceAsync("Product.Create", () => inner.CreateProductAsync(request));

    public Task<OperationResult<ProductDto>> GetProductAsync(Guid id) =>
        TraceAsync("Product.Get", () => inner.GetProductAsync(id),
            a => a?.SetTag("product.id", id));

    public Task<OperationResult<PagedResult<ProductDto>>> GetProductsAsync(int page, int pageSize, string? search = null) =>
        TraceAsync("Product.GetAll", () => inner.GetProductsAsync(page, pageSize, search),
            a => a?.SetTag("search", search));

    public Task<OperationResult<ProductDto>> UpdateProductAsync(Guid id, CreateProductRequest request) =>
        TraceAsync("Product.Update", () => inner.UpdateProductAsync(id, request),
            a => a?.SetTag("product.id", id));

    public Task<OperationResult<bool>> DeleteProductAsync(Guid id) =>
        TraceAsync("Product.Delete", () => inner.DeleteProductAsync(id),
            a => a?.SetTag("product.id", id));

    public Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode) =>
        TraceAsync("Product.GetByBarcode", () => inner.GetVariantByBarcodeAsync(barcode),
            a => a?.SetTag("barcode", barcode));

    public Task<OperationResult<ProductImageDto>> AddImageAsync(Guid productId, AddProductImageRequest request) =>
        TraceAsync("Product.AddImage", () => inner.AddImageAsync(productId, request),
            a => a?.SetTag("product.id", productId));

    public Task<OperationResult<bool>> RemoveImageAsync(Guid productId, Guid imageId) =>
        TraceAsync("Product.RemoveImage", () => inner.RemoveImageAsync(productId, imageId),
            a =>
            {
                a?.SetTag("product.id", productId);
                a?.SetTag("image.id", imageId);
            });

    public Task<OperationResult<bool>> SetMainImageAsync(Guid productId, Guid imageId) =>
        TraceAsync("Product.SetMainImage", () => inner.SetMainImageAsync(productId, imageId),
            a =>
            {
                a?.SetTag("product.id", productId);
                a?.SetTag("image.id", imageId);
            });

    public Task<OperationResult<ProductVariantDto>> CreateProductVariantAsync(Guid productId, CreateProductVariantRequest request) =>
        TraceAsync("Product.CreateVariant", () => inner.CreateProductVariantAsync(productId, request),
            a => a?.SetTag("product.id", productId));

    public Task<OperationResult<List<ProductVariantDto>>> GetProductVariantsAsync(Guid productId) =>
        TraceAsync("Product.GetVariants", () => inner.GetProductVariantsAsync(productId),
            a => a?.SetTag("product.id", productId));

    public Task<OperationResult<ProductVariantDto>> UpdateProductVariantAsync(Guid productId, Guid variantId, UpdateProductVariantRequest request) =>
        TraceAsync("Product.UpdateVariant", () => inner.UpdateProductVariantAsync(productId, variantId, request),
            a =>
            {
                a?.SetTag("product.id", productId);
                a?.SetTag("variant.id", variantId);
            });

    public Task<OperationResult<bool>> DeleteProductVariantAsync(Guid productId, Guid variantId) =>
        TraceAsync("Product.DeleteVariant", () => inner.DeleteProductVariantAsync(productId, variantId),
            a =>
            {
                a?.SetTag("product.id", productId);
                a?.SetTag("variant.id", variantId);
            });
}
