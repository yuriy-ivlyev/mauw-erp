using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Product repository implementation.
/// </summary>
public class ProductRepository(MauwDbContext dbContext) : Repository<Product>(dbContext), IProductRepository
{
    public override async Task<Product?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Variants)
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
        var query = _dbSet.Include(p => p.Variants).AsQueryable();

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
}