using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Invoice-specific repository operations.
/// </summary>
public interface IInvoiceRepository : IRepository<Invoice, Guid>
{
    Task<Invoice?> GetByNumberAsync(string invoiceNumber);
    Task<Invoice?> GetByOrderIdAsync(Guid orderId);
    Task AcquireOrderInvoiceLockAsync(Guid orderId);
    Task AcquireInvoiceNumberLockAsync(string prefix);
    Task<List<string>> GetInvoiceNumbersByPrefixAsync(string prefix);
    Task<IEnumerable<Invoice>> GetByContactIdAsync(Guid contactId);
    Task<(IEnumerable<Invoice> Items, int Total)> GetByContactIdPagedAsync(Guid contactId, int page, int pageSize);
    Task<(IEnumerable<Invoice> Items, int Total)> GetPagedAsync(
        Guid? contactId,
        string? search,
        int page,
        int pageSize,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        bool unpaidOnly = false,
        string? sortBy = null,
        bool sortDescending = true);
    Task<IEnumerable<Invoice>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds);
}
