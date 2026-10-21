using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class WarehouseRepository(MauwDbContext dbContext) : Repository<Warehouse, Guid>(dbContext), IWarehouseRepository
{
    public async Task<(IEnumerable<Warehouse> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        WarehouseType? type,
        bool? isActive,
        string? sortBy = null,
        bool sortDescending = false)
    {
        var query = _dbSet
            .Include(w => w.Address)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(w =>
                w.Code.ToLower().Contains(term) ||
                w.Name.ToLower().Contains(term));
        }

        if (type.HasValue)
            query = query.Where(w => w.Type == type.Value);

        if (isActive.HasValue)
            query = query.Where(w => w.IsActive == isActive.Value);

        var total = await query.CountAsync();

        var orderedQuery = sortBy?.ToLowerInvariant() switch
        {
            "name" => sortDescending ? query.OrderByDescending(w => w.Name) : query.OrderBy(w => w.Name),
            "type" => sortDescending ? query.OrderByDescending(w => w.Type) : query.OrderBy(w => w.Type),
            "isactive" => sortDescending ? query.OrderByDescending(w => w.IsActive) : query.OrderBy(w => w.IsActive),
            "isdefault" => sortDescending ? query.OrderByDescending(w => w.IsDefault) : query.OrderBy(w => w.IsDefault),
            "city" => sortDescending ? query.OrderByDescending(w => w.Address.City) : query.OrderBy(w => w.Address.City),
            _ => sortDescending ? query.OrderByDescending(w => w.Code) : query.OrderBy(w => w.Code)
        };
        var items = await orderedQuery
            .ThenBy(w => w.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Warehouse?> GetByIdWithAddressAsync(Guid id) =>
        await _dbSet
            .Include(w => w.Address)
            .FirstOrDefaultAsync(w => w.Id == id);

    public async Task<Warehouse?> GetDefaultWarehouseAsync() =>
        await _dbSet.FirstOrDefaultAsync(w => w.IsDefault);

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();

        return await _dbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(w =>
                w.Code == normalizedCode &&
                (!excludeId.HasValue || w.Id != excludeId.Value));
    }
}
