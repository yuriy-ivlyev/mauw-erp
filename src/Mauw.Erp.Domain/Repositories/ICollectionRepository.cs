using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface ICollectionRepository : IRepository<Collection, Guid>
{
    Task<Collection?> GetByNameAsync(string name);
}
