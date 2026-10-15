using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Invoice repository implementation.
/// </summary>
public class InvoiceRepository(MauwDbContext dbContext) : Repository<Invoice, Guid>(dbContext), IInvoiceRepository
{
    public async Task<Invoice?> GetByNumberAsync(string invoiceNumber)
    {
        return await _dbSet
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    public async Task<IEnumerable<Invoice>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .AsNoTracking()
            .Where(i => i.ContactId == contactId)
            .ToListAsync();
    }
}