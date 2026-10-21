using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Product repository implementation.
/// </summary>
public class ProductRepository(MauwDbContext dbContext) : Repository<Product, Guid>(dbContext), IProductRepository
{
    public override async Task<Product?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Variants).ThenInclude(v => v.Color)
            .Include(p => p.Variants).ThenInclude(v => v.Size)
            .Include(p => p.Category)
            .Include(p => p.Collection)
            .Include(p => p.Material)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product?> GetByInternalCodeAsync(string internalCode)
    {
        return await _dbSet
            .Include(p => p.Variants)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.InternalCode == internalCode);
    }

    public async Task<IEnumerable<Product>> GetPaginatedAsync(int page, int pageSize, string? search = null)
    {
        var query = _dbSet
            .Include(p => p.Variants)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchLower) || p.InternalCode.ToLower().Contains(searchLower));
        }

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(string? search = null)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchLower) || p.InternalCode.ToLower().Contains(searchLower));
        }

        return await query.CountAsync();
    }

    public async Task<IEnumerable<Product>> SearchParentsAsync(string query, int limit = 20)
    {
        var term = query.Trim().ToLower();
        return await _dbSet
            .Include(p => p.Variants)
            .ThenInclude(v => v.Color)
            .Include(p => p.Variants)
            .ThenInclude(v => v.Size)
            .AsNoTracking()
            .Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.SupplierCode.ToLower().Contains(term) ||
                p.InternalCode.ToLower().Contains(term))
            .OrderBy(p => p.Name)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdWithStockAsync(Guid id, Guid warehouseId)
    {
        return await _dbSet
            .Include(p => p.Variants)
            .ThenInclude(v => v.Color)
            .Include(p => p.Variants)
            .ThenInclude(v => v.Size)
            .Include(p => p.Variants)
            .ThenInclude(v => v.StockMovements)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product?> GetBySupplierCodeAsync(Guid supplierId, string supplierCode)
    {
        return await _dbSet
            .Include(p => p.Variants)
            .ThenInclude(v => v.Color)
            .Include(p => p.Variants)
            .ThenInclude(v => v.Size)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SupplierId == supplierId && p.SupplierCode == supplierCode);
    }
}