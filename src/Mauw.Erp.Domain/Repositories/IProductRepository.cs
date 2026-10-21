using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Product-specific repository operations.
/// </summary>
public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product?> GetByInternalCodeAsync(string internalCode);
    Task<IEnumerable<Product>> GetPaginatedAsync(int page, int pageSize, string? search = null);
    Task<int> GetTotalCountAsync(string? search = null);
    Task<IEnumerable<Product>> SearchParentsAsync(string query, int limit = 20);
    Task<Product?> GetByIdWithStockAsync(Guid id, Guid warehouseId);
    Task<Product?> GetBySupplierCodeAsync(Guid supplierId, string supplierCode);
}