using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// DdtDocument-specific repository operations.
/// </summary>
public interface IDdtDocumentRepository : IRepository<DdtDocument>
{
    Task<IEnumerable<DdtDocument>> GetByContactIdAsync(Guid contactId);
    Task<DdtDocument?> GetByNumberAsync(string ddtNumber);
}