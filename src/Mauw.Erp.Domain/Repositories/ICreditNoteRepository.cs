namespace Mauw.Erp.Domain.Repositories;

using Mauw.Erp.Domain.Entities;

public interface ICreditNoteRepository : IRepository<CreditNote, Guid>
{
    Task<IEnumerable<CreditNote>> GetByInvoiceIdsAsync(IEnumerable<Guid> invoiceIds);
    Task<List<CreditNote>> GetForStatementByContactIdAsync(
        Guid contactId,
        CancellationToken cancellationToken = default);
    Task<CreditNote?> GetForAllocationAsync(Guid creditNoteId);
    Task<List<CreditNote>> GetAvailableForContactAsync(Guid contactId);
    Task<int> CountCreditNotesByPrefixAsync(string prefix);
    Task AcquireInvoiceCreditNoteLockAsync(Guid invoiceId);
    Task AcquireCreditNoteAllocationLockAsync(Guid creditNoteId);
    Task AcquireSdiTransmissionLockAsync(Guid creditNoteId);
    Task AcquireCreditNoteNumberLockAsync(string prefix);
    Task<List<string>> GetCreditNoteNumbersByPrefixAsync(string prefix);
}
