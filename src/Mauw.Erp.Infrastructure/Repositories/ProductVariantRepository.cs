using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// ProductVariant repository implementation.
/// </summary>
public class ProductVariantRepository(MauwDbContext dbContext)
    : Repository<ProductVariant, Guid>(dbContext), IProductVariantRepository
{
    public async Task<ProductVariant?> GetByBarcodeAsync(string barcode)
    {
        return await _dbSet
            .Include(v => v.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Barcode.ToLower() == barcode.ToLower());
    }

    public async Task<IEnumerable<ProductVariant>> SearchAsync(string query, int limit = 10)
    {
        var term = query.Trim().ToLower();

        return await _dbSet
            .Include(v => v.Product)
            .Include(v => v.Color)
            .Include(v => v.Size)
            .AsNoTracking()
            .Where(v =>
                v.Barcode.ToLower().Contains(term) ||
                v.Product.Name.ToLower().Contains(term) ||
                (v.Color != null && v.Color.Name.ToLower().Contains(term)) ||
                (v.Size  != null && v.Size.Name.ToLower().Contains(term)))
            .OrderBy(v => v.Barcode.ToLower() == term ? 0 : 1)  // exact barcode first
            .ThenBy(v => v.Product.Name)
            .Take(limit)
            .ToListAsync();
    }
}