using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface ICassaSessionRepository
{
    Task<CassaSession?> GetByIdAsync(Guid id);
    Task<IEnumerable<CassaSession>> GetPausedSessionsAsync();
    Task<int> CountAllAsync();
    Task AddAsync(CassaSession session);
    Task SaveAsync();
}
