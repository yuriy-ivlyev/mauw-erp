using Mauw.Erp.Application.Settings;
using Mauw.Erp.Contracts.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Decorator con fallback automatico:
///   1. Se la chiave Google non è configurata → Photon direttamente.
///   2. Se Google fallisce (quota, 4xx/5xx, timeout) → log Warning + Photon.
/// </summary>
public class FallbackGeocodingService(
    GooglePlacesService googlePlaces,
    PhotonGeocodingService photon,
    IOptions<GooglePlacesSettings> settings,
    ILogger<FallbackGeocodingService> logger) : IGeocodingService
{
    public async Task<IEnumerable<AddressSuggestionDto>> AutocompleteAsync(
        string input, string sessionToken, string countryCode = "IT", CancellationToken ct = default)
    {
        if (!settings.Value.IsConfigured)
            return await photon.AutocompleteAsync(input, sessionToken, countryCode, ct);

        try
        {
            return await googlePlaces.AutocompleteAsync(input, sessionToken, countryCode, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Google Places Autocomplete fallito – uso Photon come fallback. Input: {Input}", input);
            return await photon.AutocompleteAsync(input, sessionToken, countryCode, ct);
        }
    }

    public async Task<GeocodedAddressDto?> GetPlaceDetailsAsync(
        string placeId, string sessionToken, CancellationToken ct = default)
    {
        if (!settings.Value.IsConfigured)
            return await photon.GetPlaceDetailsAsync(placeId, sessionToken, ct);

        // Se il placeId inizia con "osm:" proviene da Photon (usato durante un fallback)
        if (placeId.StartsWith("osm:", StringComparison.OrdinalIgnoreCase))
            return await photon.GetPlaceDetailsAsync(placeId, sessionToken, ct);

        try
        {
            return await googlePlaces.GetPlaceDetailsAsync(placeId, sessionToken, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Google Places Details fallito – fallback Photon. PlaceId: {PlaceId}", placeId);
            return await photon.GetPlaceDetailsAsync(placeId, sessionToken, ct);
        }
    }
}
