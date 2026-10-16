using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Repository per la tabella di lookup countries, con cache in memoria.
/// </summary>
public class CountryRepository(MauwDbContext dbContext, IMemoryCache cache) : ICountryRepository
{
    private const string CacheKey = "countries_all";

    public async Task<IEnumerable<Country>> GetAllOrderedAsync()
    {
        if (cache.TryGetValue(CacheKey, out IEnumerable<Country>? cached))
            return cached!;

        var countries = await dbContext.Countries
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        cache.Set(CacheKey, countries, TimeSpan.FromHours(24));
        return countries;
    }

    public async Task<Country?> FindAsync(string isoCode) =>
        await dbContext.Countries
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsoCode == isoCode.ToUpperInvariant());
}
