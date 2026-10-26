using System.Net;
using System.Text;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Settings;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class AddressAndVatProviderTests
{
    [Fact]
    public async Task VatValidationPassesThroughForUnknownAndNonEuCountries()
    {
        var (unknownService, _) = CreateVatService(null);
        var nonEuService = CreateVatService(new Country { IsoCode = "US", IsEuMember = false }).Service;

        var unknown = await unknownService.ValidateAsync(" us ", " tax-123 ");
        var nonEu = await nonEuService.ValidateAsync("us", "tax-123");

        Assert.True(unknown.IsFormatValid);
        Assert.False(unknown.IsViesAvailable);
        Assert.Null(unknown.IsViesValid);
        Assert.True(nonEu.IsFormatValid);
        Assert.False(nonEu.IsViesAvailable);
    }

    [Fact]
    public async Task InvalidItalianVatDoesNotCallVies()
    {
        var (service, factory) = CreateVatService(EuCountry("IT"));

        var result = await service.ValidateAsync("it", "12345678904");

        Assert.False(result.IsFormatValid);
        Assert.Equal("Formato P.IVA non valido per IT.", result.ErrorMessage);
        factory.Verify(item => item.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValidItalianVatIsNormalizedAndValidatedThroughVies()
    {
        HttpRequestMessage? request = null;
        var response = JsonResponse("{\"isValid\":true,\"name\":\"Societa\",\"address\":\"Via Roma\"}");
        var service = CreateVatService(EuCountry("IT"), _ =>
        {
            request = _;
            return Task.FromResult(response);
        }).Service;

        var result = await service.ValidateAsync(" it ", " 12345678903 ");

        Assert.True(result.IsFormatValid);
        Assert.True(result.IsViesAvailable);
        Assert.True(result.IsViesValid);
        Assert.Equal("Societa", result.CompanyName);
        Assert.Equal("Via Roma", result.CompanyAddress);
        Assert.Equal("ms/IT/vat/12345678903", request!.RequestUri!.AbsolutePath.TrimStart('/'));
    }

    [Theory]
    [InlineData("AT", "U12345678")]
    [InlineData("BE", "0123456789")]
    [InlineData("BG", "123456789")]
    [InlineData("CY", "12345678A")]
    [InlineData("CZ", "12345678")]
    [InlineData("DE", "123456789")]
    [InlineData("DK", "12345678")]
    [InlineData("EE", "123456789")]
    [InlineData("ES", "A12345678")]
    [InlineData("FI", "12345678")]
    [InlineData("FR", "AB123456789")]
    [InlineData("GB", "123456789")]
    [InlineData("GR", "123456789")]
    [InlineData("HR", "12345678901")]
    [InlineData("HU", "12345678")]
    [InlineData("IE", "1A23456AB")]
    [InlineData("LT", "123456789")]
    [InlineData("LU", "12345678")]
    [InlineData("LV", "12345678901")]
    [InlineData("MT", "12345678")]
    [InlineData("NL", "123456789B01")]
    [InlineData("PL", "1234567890")]
    [InlineData("PT", "123456789")]
    [InlineData("RO", "123456")]
    [InlineData("SE", "123456789012")]
    [InlineData("SI", "12345678")]
    [InlineData("SK", "1234567890")]
    public async Task EuVatFormatIsCheckedForEverySupportedCountry(string countryCode, string vatNumber)
    {
        var service = CreateVatService(EuCountry(countryCode),
            _ => Task.FromResult(JsonResponse("{\"isValid\":true}"))).Service;

        var result = await service.ValidateAsync(countryCode, vatNumber);

        Assert.True(result.IsFormatValid);
        Assert.True(result.IsViesAvailable);
    }

    [Fact]
    public async Task InvalidEuFormatAndViesNegativeOrEmptyResponsesAreReported()
    {
        var invalidFormatService = CreateVatService(EuCountry("DE")).Service;
        var invalidFormat = await invalidFormatService.ValidateAsync("DE", "123");
        Assert.False(invalidFormat.IsFormatValid);

        var invalidViesService = CreateVatService(EuCountry("DE"),
            _ => Task.FromResult(JsonResponse("{\"isValid\":false}"))).Service;
        var invalidVies = await invalidViesService.ValidateAsync("DE", "123456789");
        Assert.True(invalidVies.IsFormatValid);
        Assert.False(invalidVies.IsViesValid);
        Assert.Equal("P.IVA non trovata nel registro VIES.", invalidVies.ErrorMessage);

        var emptyResponseService = CreateVatService(EuCountry("DE"),
            _ => Task.FromResult(JsonResponse("null"))).Service;
        var empty = await emptyResponseService.ValidateAsync("DE", "123456789");
        Assert.False(empty.IsViesAvailable);
        Assert.Equal("Risposta VIES non valida.", empty.ErrorMessage);
    }

    [Fact]
    public async Task ViesTransportErrorsReturnUnavailableResults()
    {
        var requestFailure = CreateVatService(EuCountry("DE"),
            _ => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))).Service;
        var timeout = CreateVatService(EuCountry("DE"),
            _ => Task.FromException<HttpResponseMessage>(new TaskCanceledException())).Service;

        var unavailable = await requestFailure.ValidateAsync("DE", "123456789");
        var timedOut = await timeout.ValidateAsync("DE", "123456789");

        Assert.True(unavailable.IsFormatValid);
        Assert.False(unavailable.IsViesAvailable);
        Assert.Equal("Servizio VIES temporaneamente non disponibile.", unavailable.ErrorMessage);
        Assert.False(timedOut.IsViesAvailable);
        Assert.Equal("Timeout VIES.", timedOut.ErrorMessage);
    }

    [Fact]
    public async Task GoogleAutocompleteMapsPredictionsAndSendsRegion()
    {
        string? requestBody = null;
        var service = CreateGoogleService(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""
                {"suggestions":[
                  {"placePrediction":{"placeId":"place-1","text":{"text":"Via Roma 1"},
                    "structuredFormat":{"mainText":{"text":"Via Roma"},"secondaryText":{"text":"Roma"}}}},
                  {"placePrediction":null}
                ]}
                """);
        });

        var result = (await service.AutocompleteAsync("Via Roma", "session", "it")).ToArray();

        var suggestion = Assert.Single(result);
        Assert.Equal("place-1", suggestion.PlaceId);
        Assert.Equal("Via Roma 1", suggestion.Text);
        Assert.Equal("Via Roma", suggestion.MainText);
        Assert.Equal("Roma", suggestion.SecondaryText);
        Assert.Null(suggestion.PrePopulatedAddress);
        Assert.Contains("\"includedRegionCodes\":[\"IT\"]", requestBody);
    }

    [Fact]
    public async Task GoogleAutocompleteRejectsUnsuccessfulResponses()
    {
        var service = CreateGoogleService(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad request")
            }));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.AutocompleteAsync("x", "session"));
    }

    [Fact]
    public async Task GooglePlaceDetailsMapsAddressAndFallsBackWhenComponentsAreMissing()
    {
        var service = CreateGoogleService(_ => Task.FromResult(JsonResponse("""
            {"id":"place-1","formattedAddress":"Via Roma 1, Roma",
             "addressComponents":[
               {"longText":"Via Roma","shortText":"Via Roma","types":["route"]},
               {"longText":"1","shortText":"1","types":["street_number"]},
               {"longText":"00100","shortText":"00100","types":["postal_code"]},
               {"longText":"Roma","shortText":"Roma","types":["locality"]},
               {"longText":"Roma","shortText":"RM","types":["administrative_area_level_2"]},
               {"longText":"Italia","shortText":"it","types":["country"]}
             ],
             "location":{"latitude":41.9,"longitude":12.5}}
            """)));

        var result = await service.GetPlaceDetailsAsync("place/1", "session");

        Assert.NotNull(result);
        Assert.Equal("Via Roma 1", result.Street);
        Assert.Equal("00100", result.ZipCode);
        Assert.Equal("Roma", result.City);
        Assert.Equal("RM", result.Province);
        Assert.Equal("IT", result.CountryCode);
        Assert.Equal("Via Roma 1, Roma", result.FormattedAddress);
        Assert.Equal("place-1", result.ExternalPlaceId);
        Assert.Equal(41.9, result.Latitude);
        Assert.Equal(12.5, result.Longitude);
    }

    [Fact]
    public async Task GooglePlaceDetailsHandlesMissingPayloadAndHttpErrors()
    {
        var emptyService = CreateGoogleService(_ =>
            Task.FromResult(JsonResponse("null")));
        Assert.Null(await emptyService.GetPlaceDetailsAsync("id", "session"));

        var errorService = CreateGoogleService(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("unavailable")
            }));
        await Assert.ThrowsAsync<HttpRequestException>(() => errorService.GetPlaceDetailsAsync("id", "session"));
    }

    [Fact]
    public async Task PhotonAutocompleteMapsFeaturesCachesAddressesAndUsesFallbackFields()
    {
        var (service, cache) = CreatePhotonService(_ => Task.FromResult(JsonResponse("""
            {"features":[
              {"geometry":{"coordinates":[12.5,41.9]},
               "properties":{"osm_id":123,"street":"Via Roma","housenumber":"1","postcode":"00100",
                 "city":"Roma","county":"RM","country_code":"it"}},
              {"geometry":null,
               "properties":{"name":"Piazza","town":"Milano","state":"Lombardia"}},
              {"geometry":{"coordinates":[1,2]},"properties":null}
            ]}
            """)));

        var suggestions = (await service.AutocompleteAsync("Via Roma", "session")).ToArray();
        var first = suggestions[0];
        var second = suggestions[1];

        Assert.Equal(2, suggestions.Length);
        Assert.Equal("osm:123", first.PlaceId);
        Assert.Equal("Via Roma 1, 00100, Roma", first.Text);
        Assert.Equal("Via Roma 1", first.MainText);
        Assert.Equal("IT", first.PrePopulatedAddress!.CountryCode);
        Assert.Equal(12.5, first.PrePopulatedAddress.Longitude);
        Assert.Equal(41.9, first.PrePopulatedAddress.Latitude);
        Assert.Equal(first.PrePopulatedAddress, await service.GetPlaceDetailsAsync("osm:123", "session"));
        Assert.Equal("Piazza", second.MainText);
        Assert.Equal("Milano", second.PrePopulatedAddress!.City);
        Assert.Equal(0d, second.PrePopulatedAddress.Latitude);
        Assert.Null(await service.GetPlaceDetailsAsync("missing", "session"));
        Assert.NotNull(cache);
    }

    [Fact]
    public async Task PhotonReturnsEmptySuggestionsWhenTheProviderFails()
    {
        var service = CreatePhotonService(_ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))).Service;

        Assert.Empty(await service.AutocompleteAsync("Via Roma", "session"));
    }

    [Fact]
    public async Task GeocodingFallbackUsesPhotonWithoutKeyAndAfterGoogleFailures()
    {
        var photon = CreatePhotonService(_ => Task.FromResult(JsonResponse("""
            {"features":[{"geometry":{"coordinates":[12,41]},"properties":{"osm_id":55,"city":"Roma"}}]}
            """))).Service;
        var google = CreateGoogleService(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var noKey = new FallbackGeocodingService(google, photon,
            Options.Create(new GooglePlacesSettings()), NullLogger<FallbackGeocodingService>.Instance);

        var fromPhoton = await noKey.AutocompleteAsync("Roma", "session");
        Assert.Equal("osm:55", Assert.Single(fromPhoton).PlaceId);

        var configured = new FallbackGeocodingService(google, photon,
            Options.Create(new GooglePlacesSettings { ApiKey = "configured" }),
            NullLogger<FallbackGeocodingService>.Instance);
        var afterGoogleFailure = await configured.AutocompleteAsync("Roma", "session");
        Assert.Equal("osm:55", Assert.Single(afterGoogleFailure).PlaceId);
        Assert.NotNull(await configured.GetPlaceDetailsAsync("osm:55", "session"));
    }

    [Fact]
    public async Task GeocodingDetailsUseGoogleThenFallBackToPhotonOnErrors()
    {
        var photon = CreatePhotonService(_ => Task.FromResult(JsonResponse("""
            {"features":[{"geometry":{"coordinates":[12,41]},"properties":{"osm_id":55,"city":"Roma"}}]}
            """))).Service;
        await photon.AutocompleteAsync("Roma", "session");
        var google = CreateGoogleService(_ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));
        var fallback = new FallbackGeocodingService(google, photon,
            Options.Create(new GooglePlacesSettings { ApiKey = "configured" }),
            NullLogger<FallbackGeocodingService>.Instance);

        Assert.Null(await fallback.GetPlaceDetailsAsync("place-1", "session"));
        Assert.NotNull(await fallback.GetPlaceDetailsAsync("osm:55", "session"));
    }

    private static Country EuCountry(string code) => new() { IsoCode = code, IsEuMember = true };

    private static (VatValidationService Service, Mock<IHttpClientFactory> Factory) CreateVatService(
        Country? country,
        Func<HttpRequestMessage, Task<HttpResponseMessage>>? handler = null)
    {
        var countries = new Mock<ICountryRepository>();
        countries.Setup(repository => repository.FindAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => country?.IsoCode == code ? country : null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(work => work.Countries).Returns(countries.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("ViesApi"))
            .Returns(CreateClient(handler ?? (_ => Task.FromResult(JsonResponse("{\"isValid\":true}")))));
        return (new VatValidationService(factory.Object, unitOfWork.Object,
            NullLogger<VatValidationService>.Instance), factory);
    }

    private static GooglePlacesService CreateGoogleService(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
        new(CreateFactory("GooglePlacesClient", handler), NullLogger<GooglePlacesService>.Instance);

    private static (PhotonGeocodingService Service, IMemoryCache Cache) CreatePhotonService(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new PhotonGeocodingService(CreateFactory("PhotonClient", handler), cache,
            NullLogger<PhotonGeocodingService>.Instance), cache);
    }

    private static IHttpClientFactory CreateFactory(
        string name,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient(name)).Returns(CreateClient(handler));
        return factory.Object;
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
        new(new StubHandler(handler)) { BaseAddress = new Uri("https://example.test/") };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}
