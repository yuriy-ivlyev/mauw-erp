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

    public Task<OperationResult<bool>> SetVarianteVetrinaAsync(Guid productId, Guid variantId, bool value) =>
        TraceAsync("Product.SetVarianteVetrina", () => inner.SetVarianteVetrinaAsync(productId, variantId, value),
            a =>
            {
                a?.SetTag("product.id", productId);
                a?.SetTag("variant.id", variantId);
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

    public Task<OperationResult<List<ProductParentDto>>> SearchParentProductsAsync(string query, int limit = 20) =>
        TraceAsync("Product.SearchParents", () => inner.SearchParentProductsAsync(query, limit),
            a => a?.SetTag("query", query));

    public Task<OperationResult<ProductMatrixDto>> GetProductMatrixAsync(Guid productId, Guid warehouseId) =>
        TraceAsync("Product.GetMatrix", () => inner.GetProductMatrixAsync(productId, warehouseId),
            a =>
            {
                a?.SetTag("product.id", productId);
                a?.SetTag("warehouse.id", warehouseId);
            });
}
