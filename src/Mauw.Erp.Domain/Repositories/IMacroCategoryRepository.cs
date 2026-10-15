using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface IMacroCategoryRepository : IRepository<MacroCategory, Guid>
{
    Task<MacroCategory?> GetByNameAsync(string name);
}
