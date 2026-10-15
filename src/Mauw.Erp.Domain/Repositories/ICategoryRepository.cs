using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface ICategoryRepository : IRepository<Category, Guid>
{
    Task<Category?> GetByNameAsync(string name);
}
