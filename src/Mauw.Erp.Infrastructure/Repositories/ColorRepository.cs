using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class ColorRepository(MauwDbContext dbContext) : Repository<Color>(dbContext), IColorRepository
{
    public async Task<Color?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
    }
}
