using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Geocoding endpoints: autocomplete indirizzi e dettaglio posto.
/// Delega a IGeocodingService (Google Places con fallback automatico su Photon/OSM).
/// </summary>
public static class GeoEndpoints
{
    public static void MapGeoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/geo")
            .WithName("Geo");

        group.MapGet("/autocomplete", Autocomplete)
            .WithName("AddressAutocomplete")
            .WithSummary("Suggerimenti autocomplete indirizzo (Google Places / OSM fallback)");

        group.MapGet("/place-details", PlaceDetails)
            .WithName("PlaceDetails")
            .WithSummary("Dettagli geocodificati di un posto selezionato (solo provider Google)");
    }

    private static async Task<Ok<IEnumerable<AddressSuggestionDto>>> Autocomplete(
        string input,
        string sessionToken,
        IGeocodingService geoService,
        string countryCode = "IT",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length < 3)
            return TypedResults.Ok(Enumerable.Empty<AddressSuggestionDto>());

        var results = await geoService.AutocompleteAsync(input, sessionToken, countryCode, ct);
        return TypedResults.Ok(results);
    }

    private static async Task<Results<Ok<GeocodedAddressDto>, NotFound>> PlaceDetails(
        string placeId,
        string sessionToken,
        IGeocodingService geoService,
        CancellationToken ct = default)
    {
        var result = await geoService.GetPlaceDetailsAsync(placeId, sessionToken, ct);
        return result is not null
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();
    }
}
