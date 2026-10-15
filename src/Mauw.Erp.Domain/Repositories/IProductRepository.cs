using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Product-specific repository operations.
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    Task AddImageAsync(ProductImage image);
    Task<Product?> GetByInternalCodeAsync(string internalCode);
    Task<IEnumerable<Product>> GetPaginatedAsync(int page, int pageSize, string? search = null);
    Task<int> GetTotalCountAsync(string? search = null);
}