namespace Mauw.Erp.Domain.Repositories;

using Mauw.Erp.Domain.Entities;

public interface ICreditNoteRepository : IRepository<CreditNote, Guid>
{
    Task<IEnumerable<CreditNote>> GetByInvoiceIdsAsync(IEnumerable<Guid> invoiceIds);
    Task<int> CountCreditNotesByPrefixAsync(string prefix);
}
