using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class MaterialRepository(MauwDbContext dbContext) : Repository<Material, Guid>(dbContext), IMaterialRepository
{
    public async Task<Material?> GetByNameAsync(string name)
    {
        return await _dbSet
            .Include(m => m.Composition)
            .FirstOrDefaultAsync(m => m.Name.ToLower() == name.ToLower());
    }

    public async Task<Material?> GetWithCompositionAsync(Guid id)
    {
        return await _dbSet
            .Include(m => m.Composition)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}
