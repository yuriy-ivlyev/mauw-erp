using System.Globalization;
using Mauw.Erp.Domain.Accounting;

namespace Mauw.Erp.Domain.Services;

public enum CustomerTaxRegime
{
    Domestic,
    IntraEu,
    ExtraEu
}

/// <summary>
/// Tax regime of a customer derived from its country. <see cref="ForcedVatRate"/> and
/// <see cref="ForcedVatNature"/> are null when the regime does not constrain the document lines.
/// </summary>
public sealed record CustomerTaxResolution(
    string CountryCode,
    CustomerTaxRegime Regime,
    decimal? ForcedVatRate,
    string? ForcedVatNature)
{
    public bool IsVatForced => ForcedVatRate.HasValue;
}

/// <summary>VAT classification of a line; null values mean "not constrained, use the article default".</summary>
public sealed record FiscalClassification(decimal? VatRate, string? NatureCode);

/// <summary>
/// Pure, deterministic rule that maps the customer country to the mandatory VAT classification of
/// order lines and shipping charges: no forcing for Italy, 0% N3.2 for other EU member states,
/// 0% N3.1 for any other country. It never performs the VAT calculation itself, which stays in
/// <see cref="ItalianAccountingCalculator"/>.
/// </summary>
public static class CustomerTaxPolicy
{
    public const string DomesticCountryCode = "IT";

    /// <summary>Non taxable intra-community supply.</summary>
    public const string IntraEuNature = "N3.2";

    /// <summary>Non taxable export to a non-EU country.</summary>
    public const string ExtraEuNature = "N3.1";

    // Member states other than Italy (ISO 3166-1 alpha-2).
    private static readonly HashSet<string> IntraEuCountries =
    [
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU", "IE",
        "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK"
    ];

    /// <summary>True for an EU member state other than Italy; accepts the VIES prefix EL as Greece.</summary>
    public static bool IsIntraEuCountry(string? countryCode) =>
        TryNormalizeCountry(countryCode, out var country) && IntraEuCountries.Contains(country);

    /// <summary>Blank country means Italy, as for <c>Contact.CountryCode</c>; malformed codes are rejected.</summary>
    public static AccountingResult<CustomerTaxResolution> Resolve(string? countryCode)
    {
        if (!TryNormalizeCountry(countryCode, out var country))
            return AccountingResult<CustomerTaxResolution>.Fail(
                $"Codice paese del cliente non valido ('{countryCode}'): atteso ISO 3166-1 alpha-2.");

        if (country == DomesticCountryCode)
            return AccountingResult<CustomerTaxResolution>.Ok(
                new(country, CustomerTaxRegime.Domestic, null, null));

        return AccountingResult<CustomerTaxResolution>.Ok(IntraEuCountries.Contains(country)
            ? new(country, CustomerTaxRegime.IntraEu, 0m, IntraEuNature)
            : new(country, CustomerTaxRegime.ExtraEu, 0m, ExtraEuNature));
    }

    public static AccountingResult<FiscalClassification> Classify(
        string? countryCode, decimal? requestedVatRate, string? requestedNatureCode)
    {
        var resolution = Resolve(countryCode);
        return resolution.Success
            ? Classify(resolution.Data!, requestedVatRate, requestedNatureCode)
            : AccountingResult<FiscalClassification>.Fail(resolution.Error);
    }

    /// <summary>
    /// Domestic customers keep the requested values. For foreign customers a missing value is replaced by the
    /// forced one, while an explicit different rate or Nature is rejected.
    /// </summary>
    public static AccountingResult<FiscalClassification> Classify(
        CustomerTaxResolution resolution, decimal? requestedVatRate, string? requestedNatureCode)
    {
        if (!resolution.IsVatForced)
            return AccountingResult<FiscalClassification>.Ok(new(requestedVatRate, requestedNatureCode));

        var forcedRate = resolution.ForcedVatRate!.Value;
        var forcedNature = resolution.ForcedVatNature!;
        var label = resolution.Regime == CustomerTaxRegime.IntraEu ? "intra-UE" : "extra-UE";
        if (requestedVatRate.HasValue && requestedVatRate.Value != forcedRate)
            return AccountingResult<FiscalClassification>.Fail(
                $"Cliente {label} ({resolution.CountryCode}): l'aliquota IVA ammessa è {forcedRate.ToString("0.##", CultureInfo.InvariantCulture)}% " +
                $"con Natura {forcedNature}, non {requestedVatRate.Value.ToString("0.##", CultureInfo.InvariantCulture)}%.");

        var requestedNature = string.IsNullOrWhiteSpace(requestedNatureCode)
            ? null
            : requestedNatureCode.Trim().ToUpperInvariant();
        if (requestedNature is not null && requestedNature != forcedNature)
            return AccountingResult<FiscalClassification>.Fail(
                $"Cliente {label} ({resolution.CountryCode}): la Natura IVA ammessa è {forcedNature}, non {requestedNature}.");

        return AccountingResult<FiscalClassification>.Ok(new(forcedRate, forcedNature));
    }

    private static bool TryNormalizeCountry(string? countryCode, out string country)
    {
        country = string.IsNullOrWhiteSpace(countryCode)
            ? DomesticCountryCode
            : countryCode.Trim().ToUpperInvariant();
        if (country == "EL")
            country = "GR";
        return country.Length == 2 && country.All(char.IsAsciiLetterUpper);
    }
}
