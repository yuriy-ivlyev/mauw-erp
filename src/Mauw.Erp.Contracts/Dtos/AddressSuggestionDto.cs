namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// Voce di autocomplete restituita da GooglePlaces o Photon.
/// Se PrePopulatedAddress è valorizzato (Photon), il frontend non deve
/// chiamare l'endpoint place-details.
/// </summary>
public record AddressSuggestionDto(
    /// <summary>Google Place ID o chiave opaca per provider OSM.</summary>
    string PlaceId,
    /// <summary>Testo completo del suggerimento (da mostrare nella tendina).</summary>
    string Text,
    /// <summary>Testo principale – via + numero civico.</summary>
    string MainText,
    /// <summary>Testo secondario – CAP, comune, provincia.</summary>
    string SecondaryText,
    /// <summary>
    /// Indirizzo geocodificato completo già disponibile (provider Photon/OSM).
    /// Null per Google: l'app deve fare una chiamata separata a place-details.
    /// </summary>
    GeocodedAddressDto? PrePopulatedAddress = null
);
