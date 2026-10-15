using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface IColorRepository : IRepository<Color>
{
    Task<Color?> GetByNameAsync(string name);
}
