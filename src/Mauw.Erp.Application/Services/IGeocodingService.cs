using Mauw.Erp.Contracts.Dtos;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Interfaccia unificata per geocoding / autocomplete indirizzi.
/// Implementata da GooglePlacesService, PhotonGeocodingService e
/// FallbackGeocodingService (decorator con fallback automatico).
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Restituisce suggerimenti di autocomplete per l'input parziale.
    /// </summary>
    Task<IEnumerable<AddressSuggestionDto>> AutocompleteAsync(
        string input,
        string sessionToken,
        string countryCode = "IT",
        CancellationToken ct = default);

    /// <summary>
    /// Restituisce i dettagli completi e geocodificati di un posto già selezionato.
    /// Viene chiamato solo se AddressSuggestionDto.PrePopulatedAddress è null (Google).
    /// </summary>
    Task<GeocodedAddressDto?> GetPlaceDetailsAsync(
        string placeId,
        string sessionToken,
        CancellationToken ct = default);
}
