using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Invoice-specific repository operations.
/// </summary>
public interface IInvoiceRepository : IRepository<Invoice, Guid>
{
    Task<Invoice?> GetByNumberAsync(string invoiceNumber);
    Task<IEnumerable<Invoice>> GetByContactIdAsync(Guid contactId);
    Task<(IEnumerable<Invoice> Items, int Total)> GetByContactIdPagedAsync(Guid contactId, int page, int pageSize);
    Task<IEnumerable<Invoice>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds);
    Task<int> CountInvoicesByPrefixAsync(string prefix);
}
