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
    Task<OperationResult<List<ProductParentDto>>> SearchParentProductsAsync(string query, int limit = 20);
    Task<OperationResult<ProductMatrixDto>> GetProductMatrixAsync(Guid productId, Guid warehouseId);

    Task<OperationResult<ProductVariantDto>> CreateProductVariantAsync(Guid productId, CreateProductVariantRequest request);
    Task<OperationResult<List<ProductVariantDto>>> GetProductVariantsAsync(Guid productId);
    Task<OperationResult<ProductVariantDto>> UpdateProductVariantAsync(Guid productId, Guid variantId, UpdateProductVariantRequest request);
    Task<OperationResult<bool>> DeleteProductVariantAsync(Guid productId, Guid variantId);

    /// <summary>Marks (or unmarks) the variant whose images represent the product in lists; at most one per product.</summary>
    Task<OperationResult<bool>> SetVarianteVetrinaAsync(Guid productId, Guid variantId, bool value);
}