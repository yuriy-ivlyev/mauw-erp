using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Product application service - business logic layer.
/// </summary>
public interface IProductService
{
    Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request);
    Task<OperationResult<ProductDto>> GetProductAsync(Guid id);
    Task<OperationResult<PagedResult<ProductDto>>> GetProductsAsync(int page, int pageSize, string? search = null);
    Task<OperationResult<ProductDto>> UpdateProductAsync(Guid id, CreateProductRequest request);
    Task<OperationResult<bool>> DeleteProductAsync(Guid id);
    Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode);

    Task<OperationResult<ProductImageDto>> AddImageAsync(Guid productId, AddProductImageRequest request);
    Task<OperationResult<bool>> RemoveImageAsync(Guid productId, Guid imageId);
    Task<OperationResult<bool>> SetMainImageAsync(Guid productId, Guid imageId);

    Task<OperationResult<ProductVariantDto>> CreateProductVariantAsync(Guid productId, CreateProductVariantRequest request);
    Task<OperationResult<List<ProductVariantDto>>> GetProductVariantsAsync(Guid productId);
    Task<OperationResult<bool>> DeleteProductVariantAsync(Guid productId, Guid variantId);
}