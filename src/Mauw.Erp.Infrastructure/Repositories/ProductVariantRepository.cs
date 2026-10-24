using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// ProductVariant repository implementation.
/// </summary>
public class ProductVariantRepository : Repository<ProductVariant, Guid>, IProductVariantRepository
{
    private readonly MauwDbContext dbContext;

    public ProductVariantRepository(MauwDbContext dbContext) : base(dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ProductVariant?> GetByBarcodeAsync(string barcode)
    {
        var variant = await _dbSet
            .Include(v => v.Product)
            .Include(v => v.Color)
            .Include(v => v.Size)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Barcode.ToLower() == barcode.ToLower());

        if (variant is not null)
            return variant;

        var simpleProductId = await dbContext.Products
            .AsNoTracking()
            .Where(p => !p.HasVariants && p.Barcode.ToLower() == barcode.ToLower())
            .Select(p => p.Id)
            .FirstOrDefaultAsync();
        if (simpleProductId == Guid.Empty)
            return null;

        return await _dbSet
            .Include(v => v.Product)
            .Include(v => v.Color)
            .Include(v => v.Size)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ProductId == simpleProductId);
    }

    public async Task<IEnumerable<ProductVariant>> SearchAsync(string query, int limit = 10, Guid? supplierId = null)
    {
        var term = query.Trim().ToLower();
        var simpleProductIds = await dbContext.Products
            .AsNoTracking()
            .Where(p => !p.HasVariants && p.Barcode.ToLower().Contains(term)
                && (!supplierId.HasValue || p.SupplierId == supplierId.Value))
            .Select(p => p.Id)
            .Take(limit)
            .ToListAsync();

        var variantQuery = _dbSet
            .Include(v => v.Product)
            .Include(v => v.Color)
            .Include(v => v.Size)
            .AsNoTracking();

        if (supplierId.HasValue)
            variantQuery = variantQuery.Where(v => v.SupplierId == supplierId.Value);

        return await variantQuery
            .Where(v =>
                v.Barcode.ToLower().Contains(term) ||
                simpleProductIds.Contains(v.ProductId) ||
                v.Product.Name.ToLower().Contains(term) ||
                (v.SupplierCode != null && v.SupplierCode.ToLower().Contains(term)) ||
                v.Product.SupplierCode.ToLower().Contains(term) ||
                (v.Color != null && v.Color.Name.ToLower().Contains(term)) ||
                (v.Size  != null && v.Size.Name.ToLower().Contains(term)))
            .OrderBy(v => v.Barcode.ToLower() == term ? 0 : 1)
            .ThenBy(v => simpleProductIds.Contains(v.ProductId) ? 0 : 1)
            .ThenBy(v => v.Product.Name)
            .Take(limit)
            .ToListAsync();
    }
}