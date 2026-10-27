using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Mauw.Erp.Contracts.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Provider Photon / OpenStreetMap (gratuito, senza chiave API).
/// Poiché Photon non ha un endpoint "place-details", tutti i dati vengono
/// inclusi direttamente in AddressSuggestionDto.PrePopulatedAddress.
/// </summary>
public class PhotonGeocodingService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<PhotonGeocodingService> logger) : IGeocodingService
{
    public async Task<IEnumerable<AddressSuggestionDto>> AutocompleteAsync(
        string input, string sessionToken, string countryCode = "IT", CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("PhotonClient");
        var url = $"api/?q={Uri.EscapeDataString(input)}&limit=6&countrycode={countryCode.ToLower()}";

        PhotonResponse? result;
        try
        {
            result = await client.GetFromJsonAsync<PhotonResponse>(url, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Photon API error");
            return [];
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Photon API error");
            return [];
        }

        var suggestions = new List<AddressSuggestionDto>();
        foreach (var f in result?.Features ?? [])
        {
            var geo = ParseFeature(f);
            if (geo is null) continue;

            // Usa l'OSM ID come placeId per eventuali lookup in cache
            var placeId = f.Properties?.OsmId is { } osmId ? $"osm:{osmId}" : $"osm:{Guid.NewGuid():N}";
            cache.Set(placeId, geo, TimeSpan.FromMinutes(10));

            var mainText = geo.Street.Length > 0
                ? geo.Street
                : f.Properties?.Name ?? geo.City;
            var secondary = $"{geo.ZipCode} {geo.City}"
                + (geo.Province is { Length: > 0 } p ? $" ({p})" : "");

            suggestions.Add(new AddressSuggestionDto(
                PlaceId: placeId,
                Text: geo.FormattedAddress,
                MainText: mainText,
                SecondaryText: secondary,
                PrePopulatedAddress: geo));
        }
        return suggestions;
    }

    public Task<GeocodedAddressDto?> GetPlaceDetailsAsync(
        string placeId, string sessionToken, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // I dati sono già in PrePopulatedAddress; ma se qualcuno chiede esplicitamente,
        // restituiamo dalla cache.
        cache.TryGetValue(placeId, out GeocodedAddressDto? dto);
        return Task.FromResult(dto);
    }

    private static GeocodedAddressDto? ParseFeature(PhotonFeature f)
    {
        var p = f.Properties;
        if (p is null) return null;

        var (lon, lat) = f.Geometry?.Coordinates is [var lo, var la] ? (lo, la) : (0.0, 0.0);
        var streetBase = p.Street ?? p.Name ?? "";
        var street     = p.HouseNumber is { Length: > 0 } hn ? $"{streetBase} {hn}".Trim() : streetBase;
        var city       = p.City ?? p.Town ?? p.Village ?? "";
        var province   = p.County ?? p.State ?? "";
        var parts      = new[] { street, p.Postcode, city }.Where(s => !string.IsNullOrWhiteSpace(s));

        return new(
            Street:          street,
            ZipCode:         p.Postcode ?? "",
            City:            city,
            Province:        province.Length > 0 ? province : null,
            CountryCode:     (p.CountryCode ?? "IT").ToUpperInvariant(),
            FormattedAddress:string.Join(", ", parts),
            ExternalPlaceId: p.OsmId?.ToString(),
            Latitude:  lat,
            Longitude: lon
        );
    }

    // ── JSON models ───────────────────────────────────────────────────────────

    private sealed record PhotonResponse(
        [property: JsonPropertyName("features")] List<PhotonFeature>? Features);

    private sealed record PhotonFeature(
        [property: JsonPropertyName("geometry")]   PhotonGeometry? Geometry,
        [property: JsonPropertyName("properties")] PhotonProperties? Properties);

    private sealed record PhotonGeometry(
        [property: JsonPropertyName("coordinates")] double[]? Coordinates);

    private sealed record PhotonProperties(
        [property: JsonPropertyName("osm_id")]       long? OsmId,
        [property: JsonPropertyName("name")]         string? Name,
        [property: JsonPropertyName("street")]       string? Street,
        [property: JsonPropertyName("housenumber")]  string? HouseNumber,
        [property: JsonPropertyName("postcode")]     string? Postcode,
        [property: JsonPropertyName("city")]         string? City,
        [property: JsonPropertyName("town")]         string? Town,
        [property: JsonPropertyName("village")]      string? Village,
        [property: JsonPropertyName("state")]        string? State,
        [property: JsonPropertyName("county")]       string? County,
        [property: JsonPropertyName("country_code")] string? CountryCode);
}
