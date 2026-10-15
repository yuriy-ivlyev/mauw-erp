using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class SizeRepository(MauwDbContext dbContext) : Repository<Size, string>(dbContext), ISizeRepository
{
    public async Task<Size?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
    }
}
