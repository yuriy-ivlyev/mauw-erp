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
}