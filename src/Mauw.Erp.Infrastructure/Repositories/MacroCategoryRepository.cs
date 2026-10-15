using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class MacroCategoryRepository(MauwDbContext dbContext) : Repository<MacroCategory>(dbContext), IMacroCategoryRepository
{
    public async Task<MacroCategory?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(mc => mc.Name.ToLower() == name.ToLower());
    }
}
