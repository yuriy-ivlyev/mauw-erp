using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Lookup endpoint: nazioni ISO, validazione P.IVA / VIES.
/// </summary>
public static class LookupsEndpoints
{
    public static void MapLookupsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/lookups")
            .WithName("Lookups");

        group.MapGet("/countries", GetCountries)
            .WithName("GetCountries")
            .WithSummary("Lista nazioni ISO 3166-1 alpha-2 ordinata per priorità");
    }

    private static async Task<Ok<List<CountryDto>>> GetCountries(
        IUnitOfWork uow,
        IMemoryCache cache)
    {
        const string key = "api_countries";
        if (!cache.TryGetValue(key, out List<CountryDto>? dtos))
        {
            var countries = await uow.Countries.GetAllOrderedAsync();
            dtos = countries.Select(c => new CountryDto(c.IsoCode, c.Name, c.IsEuMember, c.DisplayOrder)).ToList();
            cache.Set(key, dtos, TimeSpan.FromHours(24));
        }
        return TypedResults.Ok(dtos!);
    }
}
