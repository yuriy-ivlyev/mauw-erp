using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface ICountryRepository
{
    Task<IEnumerable<Country>> GetAllOrderedAsync();
    Task<Country?> FindAsync(string isoCode);
}
