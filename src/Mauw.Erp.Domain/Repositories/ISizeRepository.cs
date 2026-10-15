using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface ISizeRepository : IRepository<Size, string>
{
    Task<Size?> GetByNameAsync(string name);
}
