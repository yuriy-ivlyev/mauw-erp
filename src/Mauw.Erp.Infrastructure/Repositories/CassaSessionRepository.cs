using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class CassaSessionRepository(MauwDbContext dbContext) : ICassaSessionRepository
{
    public async Task<CassaSession?> GetByIdAsync(Guid id) =>
        await dbContext.CassaSessions.FindAsync(id);

    public async Task<IEnumerable<CassaSession>> GetPausedSessionsAsync() =>
        await dbContext.CassaSessions
            .Where(s => s.Status == CassaSessionStatus.Paused)
            .OrderByDescending(s => s.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();

    public async Task<int> CountAllAsync() =>
        await dbContext.CassaSessions.CountAsync();

    public async Task AddAsync(CassaSession session)
    {
        await dbContext.CassaSessions.AddAsync(session);
    }

    public async Task SaveAsync() =>
        await dbContext.SaveChangesAsync();
}
