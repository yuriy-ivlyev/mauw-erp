using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class CategoryRepository(MauwDbContext dbContext) : Repository<Category>(dbContext), ICategoryRepository
{
    public async Task<Category?> GetByNameAsync(string name)
    {
        return await _dbSet
            .Include(c => c.MacroCategory)
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
    }
}
