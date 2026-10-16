using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Validazione P.IVA / Tax ID: IT checksum, VIES per paesi UE, passthrough per Extra-UE.
/// </summary>
public partial class VatValidationService(
    IHttpClientFactory httpClientFactory,
    IUnitOfWork unitOfWork,
    ILogger<VatValidationService> logger) : IVatValidationService
{
    public async Task<VatValidationResultDto> ValidateAsync(string countryCode, string vatNumber)
    {
        countryCode = countryCode.Trim().ToUpperInvariant();
        vatNumber   = vatNumber.Trim().ToUpperInvariant();

        var country = await unitOfWork.Countries.FindAsync(countryCode);

        // ── Extra-UE: accetta qualsiasi Tax ID ──────────────────────────────
        if (country is null || !country.IsEuMember)
            return new(IsFormatValid: true, IsViesValid: null, IsViesAvailable: false,
                CompanyName: null, CompanyAddress: null, ErrorMessage: null);

        // ── Validazione formato ──────────────────────────────────────────────
        bool formatValid = countryCode switch
        {
            "IT" => IsValidItalianVat(vatNumber),
            _    => IsValidEuVatFormat(countryCode, vatNumber)
        };

        if (!formatValid)
            return new(IsFormatValid: false, IsViesValid: null, IsViesAvailable: false,
                CompanyName: null, CompanyAddress: null,
                ErrorMessage: $"Formato P.IVA non valido per {countryCode}.");

        // ── VIES ─────────────────────────────────────────────────────────────
        try
        {
            var client = httpClientFactory.CreateClient("ViesApi");
            var url = $"ms/{countryCode}/vat/{vatNumber}";
            var response = await client.GetFromJsonAsync<ViesResponse>(url);

            if (response is null)
                return new(true, null, false, null, null, "Risposta VIES non valida.");

            return new(
                IsFormatValid:   true,
                IsViesValid:     response.IsValid,
                IsViesAvailable: true,
                CompanyName:     response.Name,
                CompanyAddress:  response.Address,
                ErrorMessage:    response.IsValid ? null : "P.IVA non trovata nel registro VIES."
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "VIES non raggiungibile per {CC}/{VAT}", countryCode, vatNumber);
            return new(true, null, false, null, null, "Servizio VIES temporaneamente non disponibile.");
        }
        catch (TaskCanceledException)
        {
            return new(true, null, false, null, null, "Timeout VIES.");
        }
    }

    // ── Algoritmo checksum P.IVA italiana (11 cifre) ────────────────────────

    private static bool IsValidItalianVat(string vat)
    {
        if (vat.Length != 11) return false;
        if (!vat.All(char.IsAsciiDigit)) return false;

        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            int d = vat[i] - '0';
            if (i % 2 == 0)       // posizioni dispari (1-indexed) → 0-indexed pari
                sum += d;
            else                   // posizioni pari
            {
                d *= 2;
                sum += d < 10 ? d : d - 9;
            }
        }

        int expected = (10 - sum % 10) % 10;
        return expected == (vat[10] - '0');
    }

    // ── Validazione formato EU (regex per paese) ────────────────────────────

    private static bool IsValidEuVatFormat(string cc, string vat) =>
        cc switch
        {
            "AT" => Regex.IsMatch(vat, @"^U\d{8}$"),
            "BE" => Regex.IsMatch(vat, @"^0\d{9}$"),
            "BG" => Regex.IsMatch(vat, @"^\d{9,10}$"),
            "CY" => Regex.IsMatch(vat, @"^\d{8}[A-Z]$"),
            "CZ" => Regex.IsMatch(vat, @"^\d{8,10}$"),
            "DE" => Regex.IsMatch(vat, @"^\d{9}$"),
            "DK" => Regex.IsMatch(vat, @"^\d{8}$"),
            "EE" => Regex.IsMatch(vat, @"^\d{9}$"),
            "ES" => Regex.IsMatch(vat, @"^[A-Z0-9]\d{7}[A-Z0-9]$"),
            "FI" => Regex.IsMatch(vat, @"^\d{8}$"),
            "FR" => Regex.IsMatch(vat, @"^[A-Z0-9]{2}\d{9}$"),
            "GB" => Regex.IsMatch(vat, @"^\d{9}(\d{3})?$"),
            "GR" => Regex.IsMatch(vat, @"^\d{9}$"),
            "HR" => Regex.IsMatch(vat, @"^\d{11}$"),
            "HU" => Regex.IsMatch(vat, @"^\d{8}$"),
            "IE" => Regex.IsMatch(vat, @"^\d[A-Z0-9+*]\d{5}[A-Z]{1,2}$"),
            "LT" => Regex.IsMatch(vat, @"^(\d{9}|\d{12})$"),
            "LU" => Regex.IsMatch(vat, @"^\d{8}$"),
            "LV" => Regex.IsMatch(vat, @"^\d{11}$"),
            "MT" => Regex.IsMatch(vat, @"^\d{8}$"),
            "NL" => Regex.IsMatch(vat, @"^\d{9}B\d{2}$"),
            "PL" => Regex.IsMatch(vat, @"^\d{10}$"),
            "PT" => Regex.IsMatch(vat, @"^\d{9}$"),
            "RO" => Regex.IsMatch(vat, @"^\d{2,10}$"),
            "SE" => Regex.IsMatch(vat, @"^\d{12}$"),
            "SI" => Regex.IsMatch(vat, @"^\d{8}$"),
            "SK" => Regex.IsMatch(vat, @"^\d{10}$"),
            _    => !string.IsNullOrWhiteSpace(vat)
        };

    // ── VIES JSON model ──────────────────────────────────────────────────────

    private sealed record ViesResponse(
        [property: JsonPropertyName("isValid")]  bool   IsValid,
        [property: JsonPropertyName("name")]     string? Name,
        [property: JsonPropertyName("address")]  string? Address,
        [property: JsonPropertyName("userError")]string? UserError
    );
}
