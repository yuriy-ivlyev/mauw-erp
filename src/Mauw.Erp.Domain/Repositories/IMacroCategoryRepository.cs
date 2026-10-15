using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface IMacroCategoryRepository : IRepository<MacroCategory>
{
    Task<MacroCategory?> GetByNameAsync(string name);
}
