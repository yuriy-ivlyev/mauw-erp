namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// Indirizzo geocodificato con campi atomizzati pronti per il form anagrafica.
/// </summary>
public record GeocodedAddressDto(
    string Street,
    string ZipCode,
    string City,
    /// <summary>Sigla o nome esteso della provincia/stato.</summary>
    string? Province,
    /// <summary>Codice ISO 3166-1 alpha-2 del paese (es. "IT").</summary>
    string CountryCode,
    string FormattedAddress,
    string? ExternalPlaceId,
    double Latitude,
    double Longitude
);
