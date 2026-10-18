using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class CreditNoteRepository(MauwDbContext dbContext) : Repository<CreditNote, Guid>(dbContext), ICreditNoteRepository
{
    public async Task<IEnumerable<CreditNote>> GetByInvoiceIdsAsync(IEnumerable<Guid> invoiceIds)
        => await _dbSet.AsNoTracking().Where(c => invoiceIds.Contains(c.InvoiceId)).ToListAsync();

    public async Task<int> CountCreditNotesByPrefixAsync(string prefix)
        => await _dbSet.CountAsync(c => c.CreditNoteNumber.StartsWith(prefix));
}
