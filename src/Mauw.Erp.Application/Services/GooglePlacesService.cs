using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mauw.Erp.Application.Settings;
using Mauw.Erp.Contracts.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Provider Google Places API (New).
/// Usa session token per raggruppare chiamate e minimizzare i costi.
/// </summary>
public class GooglePlacesService(
    IHttpClientFactory httpClientFactory,
    ILogger<GooglePlacesService> logger) : IGeocodingService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── Autocomplete ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<AddressSuggestionDto>> AutocompleteAsync(
        string input, string sessionToken, string countryCode = "IT", CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("GooglePlacesClient");

        var body = new
        {
            input,
            sessionToken,
            includedRegionCodes = new[] { countryCode.ToUpperInvariant() }
        };

        using var response = await client.PostAsJsonAsync("places:autocomplete", body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Google Places Autocomplete {Code}: {Error}", (int)response.StatusCode, err);
            throw new HttpRequestException($"Google Places error {(int)response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<AutocompleteResponse>(JsonOpts, ct);
        return result?.Suggestions?
            .Where(s => s.PlacePrediction is not null)
            .Select(s => new AddressSuggestionDto(
                PlaceId: s.PlacePrediction!.PlaceId,
                Text: s.PlacePrediction.Text?.Text ?? "",
                MainText: s.PlacePrediction.StructuredFormat?.MainText?.Text ?? "",
                SecondaryText: s.PlacePrediction.StructuredFormat?.SecondaryText?.Text ?? "",
                PrePopulatedAddress: null  // Google richiede chiamata separata a place-details
            )) ?? [];
    }

    // ── Place Details ─────────────────────────────────────────────────────────

    public async Task<GeocodedAddressDto?> GetPlaceDetailsAsync(
        string placeId, string sessionToken, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("GooglePlacesClient");

        var url = $"places/{Uri.EscapeDataString(placeId)}?sessionToken={Uri.EscapeDataString(sessionToken)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Goog-FieldMask", "id,formattedAddress,addressComponents,location");

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Google Places Details {Code}: {Error}", (int)response.StatusCode, err);
            throw new HttpRequestException($"Google Places error {(int)response.StatusCode}");
        }

        var place = await response.Content.ReadFromJsonAsync<PlaceDetailsResponse>(JsonOpts, ct);
        if (place is null) return null;

        return ParseAddressComponents(place);
    }

    // ── Parsing componenti indirizzo ──────────────────────────────────────────

    private static GeocodedAddressDto ParseAddressComponents(PlaceDetailsResponse place)
    {
        var components = place.AddressComponents ?? [];

        static string? Get(IList<AddressComponent> comps, string type) =>
            comps.FirstOrDefault(c => c.Types?.Contains(type) == true)?.ShortText;

        static string? GetLong(IList<AddressComponent> comps, string type) =>
            comps.FirstOrDefault(c => c.Types?.Contains(type) == true)?.LongText;

        var streetName = GetLong(components, "route") ?? "";
        var streetNum  = Get(components, "street_number") ?? "";
        var street     = streetNum.Length > 0 ? $"{streetName} {streetNum}".Trim() : streetName;

        var zipCode    = Get(components, "postal_code") ?? "";
        var city       = GetLong(components, "locality")
                      ?? GetLong(components, "administrative_area_level_3")
                      ?? GetLong(components, "administrative_area_level_2")
                      ?? "";
        var province   = Get(components, "administrative_area_level_2")
                      ?? Get(components, "administrative_area_level_1");
        var countryIso = Get(components, "country")?.ToUpperInvariant() ?? "IT";

        return new(
            Street:          street,
            ZipCode:         zipCode,
            City:            city,
            Province:        province,
            CountryCode:     countryIso,
            FormattedAddress:place.FormattedAddress ?? "",
            ExternalPlaceId: place.Id,
            Latitude:        place.Location?.Latitude ?? 0,
            Longitude:       place.Location?.Longitude ?? 0
        );
    }

    // ── JSON models ───────────────────────────────────────────────────────────

    private sealed record AutocompleteResponse(
        [property: JsonPropertyName("suggestions")] List<SuggestionItem>? Suggestions);

    private sealed record SuggestionItem(
        [property: JsonPropertyName("placePrediction")] PlacePrediction? PlacePrediction);

    private sealed record PlacePrediction(
        [property: JsonPropertyName("placeId")]         string PlaceId,
        [property: JsonPropertyName("text")]            TextValue? Text,
        [property: JsonPropertyName("structuredFormat")]StructuredFormat? StructuredFormat);

    private sealed record TextValue([property: JsonPropertyName("text")] string Text);

    private sealed record StructuredFormat(
        [property: JsonPropertyName("mainText")]      TextValue? MainText,
        [property: JsonPropertyName("secondaryText")] TextValue? SecondaryText);

    private sealed record PlaceDetailsResponse(
        [property: JsonPropertyName("id")]                string? Id,
        [property: JsonPropertyName("formattedAddress")]  string? FormattedAddress,
        [property: JsonPropertyName("addressComponents")] List<AddressComponent>? AddressComponents,
        [property: JsonPropertyName("location")]          LatLng? Location);

    private sealed record AddressComponent(
        [property: JsonPropertyName("longText")]  string? LongText,
        [property: JsonPropertyName("shortText")] string? ShortText,
        [property: JsonPropertyName("types")]     List<string>? Types);

    private sealed record LatLng(
        [property: JsonPropertyName("latitude")]  double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);
}
