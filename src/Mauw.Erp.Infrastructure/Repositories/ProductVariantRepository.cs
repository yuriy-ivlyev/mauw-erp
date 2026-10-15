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
}