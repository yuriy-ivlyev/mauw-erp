using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Contact repository implementation.
/// </summary>
public class ContactRepository(MauwDbContext dbContext) : Repository<Contact, Guid>(dbContext), IContactRepository
{
    public async Task<Contact?> GetByVatNumberAsync(string vatNumber)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
    }

    public async Task<IEnumerable<Contact>> GetPaginatedAsync(int page, int pageSize, string? search = null)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c => c.BusinessName.ToLower().Contains(searchLower) || c.VatNumber.ToLower().Contains(searchLower));
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
            query = query.Where(c => c.BusinessName.ToLower().Contains(searchLower) || c.VatNumber.ToLower().Contains(searchLower));
        }

        return await query.CountAsync();
    }
}