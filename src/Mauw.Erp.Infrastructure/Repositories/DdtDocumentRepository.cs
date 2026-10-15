using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// DdtDocument repository implementation.
/// </summary>
public class DdtDocumentRepository(MauwDbContext dbContext) : Repository<DdtDocument, Guid>(dbContext), IDdtDocumentRepository
{
    public async Task<IEnumerable<DdtDocument>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(d => d.Items)
            .AsNoTracking()
            .Where(d => d.ContactId == contactId)
            .OrderByDescending(d => d.DdtDate)
            .ToListAsync();
    }

    public async Task<DdtDocument?> GetByNumberAsync(string ddtNumber)
    {
        return await _dbSet
            .Include(d => d.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DdtNumber == ddtNumber);
    }

    public override async Task<DdtDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(d => d.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }
}