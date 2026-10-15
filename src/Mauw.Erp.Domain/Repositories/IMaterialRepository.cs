using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface IMaterialRepository : IRepository<Material>
{
    Task<Material?> GetByNameAsync(string name);
    Task<Material?> GetWithCompositionAsync(Guid id);
}
