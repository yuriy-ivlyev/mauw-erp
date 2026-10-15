using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Contact-specific repository operations.
/// </summary>
public interface IContactRepository : IRepository<Contact, Guid>
{
    Task<Contact?> GetByVatNumberAsync(string vatNumber);
    Task<IEnumerable<Contact>> GetPaginatedAsync(int page, int pageSize, string? search = null);
    Task<int> GetTotalCountAsync(string? search = null);
}