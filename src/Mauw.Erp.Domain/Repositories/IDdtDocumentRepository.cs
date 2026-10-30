using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// DdtDocument-specific repository operations.
/// </summary>
public interface IDdtDocumentRepository : IRepository<DdtDocument, Guid>
{
    Task<IEnumerable<DdtDocument>> GetByContactIdAsync(Guid contactId);
    Task<(IEnumerable<DdtDocument> Items, int Total)> GetByContactIdPagedAsync(Guid contactId, int page, int pageSize);
    Task<DdtDocument?> GetByNumberAsync(string ddtNumber);
    Task<IEnumerable<DdtDocument>> GetByOrderIdsAsync(IEnumerable<Guid> orderIds);
    Task<IEnumerable<DdtDocument>> GetByOrderIdWithItemsAsync(Guid orderId);
    Task<int> CountDdtsByPrefixAsync(string prefix);
    Task<long> GetNextShipmentSequenceAsync();
}
