using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Builds the FatturaPA 1.2.3 ordinary-invoice (FPR12) document from the already calculated invoice model.
/// It only formats fiscal values computed by the accounting calculator: it never recomputes amounts and never
/// invents missing mandatory data, returning an explicit error instead.
/// </summary>
public static partial class FatturaPaInvoiceXmlBuilder
{
    public static readonly XNamespace FatturaPaNamespace =
        "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

    private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace DsNamespace = "http://www.w3.org/2000/09/xmldsig#";
    private const string SchemaLocation =
        "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2 " +
        "http://www.fatturapa.gov.it/export/fatturazione/sdi/fatturapa/v1.2/Schema_del_file_xml_FatturaPA_versione_1.2.xsd";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly HashSet<string> NatureCodes =
    [
        "N1", "N2.1", "N2.2", "N3.1", "N3.2", "N3.3", "N3.4", "N3.5", "N3.6", "N4", "N5",
        "N6.1", "N6.2", "N6.3", "N6.4", "N6.5", "N6.6", "N6.7", "N6.8", "N6.9", "N7"
    ];

    // Member states other than Italy (ISO 3166-1 alpha-2).
    private static readonly HashSet<string> EuropeanUnionCountries =
    [
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU", "IE",
        "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK"
    ];

    /// <summary>CodiceDestinatario mandated for customers resident outside Italy.</summary>
    public const string ForeignRecipientCode = "XXXXXXX";

    /// <summary>Non taxable intra-community supply.</summary>
    public const string IntraEuNature = "N3.2";

    /// <summary>Non taxable export to a non-EU country.</summary>
    public const string ExtraEuNature = "N3.1";

    // Placeholder CAP for foreign addresses without a five-digit postal code (the schema requires five digits).
    private const string NoPostalCode = "99999";

    private sealed record CustomerParty(
        string Country,
        (string Country, string Code)? TaxId,
        string? FiscalCode,
        string PostalCode,
        string City,
        string? Province,
        IReadOnlyList<XElement> RecipientElements,
        string? RequiredZeroRatedNature)
    {
        public static CustomerParty Invalid { get; } = new(
            "IT", null, null, string.Empty, string.Empty, null, [], null);
    }

    public static OperationResult<string> Build(InvoicePdfModel model, ItalianAccountingCalculator calculator)
    {
        var errors = new List<string>();
        var seller = model.Seller;
        var customer = model.Customer;

        if (!DocumentTypeRegex().IsMatch(model.DocumentType ?? string.Empty))
            errors.Add("Il tipo documento FatturaPA non è valido (atteso TDxx).");
        if (string.IsNullOrWhiteSpace(model.DocumentNumber) || model.DocumentNumber.Length > 20 ||
            !model.DocumentNumber.Any(char.IsDigit))
            errors.Add("Il numero documento deve contenere una cifra e al massimo 20 caratteri.");
        if (model.Lines.Count == 0)
            errors.Add("La fattura deve contenere almeno una riga.");

        var sellerVat = ParseVatId(seller.VatNumber);
        if (sellerVat is null)
            errors.Add("La partita IVA del cedente è obbligatoria.");
        var regime = RegimeRegex().Match(seller.TaxRegime ?? string.Empty);
        if (!regime.Success)
            errors.Add("Il regime fiscale del cedente deve riportare un codice RFxx.");
        ValidateParty("cedente", seller.BusinessName, seller.Address, seller.City, errors);
        ValidateItalianPostalCode("cedente", seller.PostalCode, errors);

        var customerParty = ResolveCustomer(customer, errors);

        var lines = new List<XElement>();
        for (var index = 0; index < model.Lines.Count; index++)
        {
            var line = BuildLine(
                model.Lines[index], index + 1, calculator, customerParty.RequiredZeroRatedNature, errors);
            if (line is not null)
                lines.Add(line);
        }

        var summaries = new List<XElement>();
        foreach (var summary in model.VatSummary)
        {
            var element = BuildSummary(summary, customerParty.RequiredZeroRatedNature, errors);
            if (element is not null)
                summaries.Add(element);
        }
        if (summaries.Count == 0)
            errors.Add("Il riepilogo IVA non può essere vuoto.");

        var payment = FatturaPaXmlMapper.BuildDatiPagamento(model.Payment with
        {
            Iban = NormalizeIban(model.Payment.Iban)
        });
        if (!payment.Success)
            errors.Add(payment.Error!);

        if (errors.Count > 0)
            return OperationResult<string>.Fail(
                "Impossibile generare il tracciato XML FatturaPA: " + string.Join(" ", errors));

        var header = new XElement("FatturaElettronicaHeader",
            new XElement("DatiTrasmissione",
                BuildIdFiscale("IdTrasmittente", sellerVat!.Value),
                new XElement("ProgressivoInvio", BuildProgressive(model.DocumentNumber)),
                new XElement("FormatoTrasmissione", "FPR12"),
                customerParty.RecipientElements),
            BuildSeller(seller, sellerVat.Value, regime.Value),
            BuildCustomer(customer, customerParty));

        var documentData = new XElement("DatiGeneraliDocumento",
            new XElement("TipoDocumento", model.DocumentType),
            new XElement("Divisa", model.Currency),
            new XElement("Data", model.IssueDate.ToString("yyyy-MM-dd", Invariant)),
            new XElement("Numero", model.DocumentNumber),
            new XElement("ImportoTotaleDocumento", Money(model.Totals.TotalGross)));
        foreach (var chunk in Chunk(Sanitize(model.FiscalNotes), 200))
            documentData.Add(new XElement("Causale", chunk));

        var body = new XElement("FatturaElettronicaBody",
            new XElement("DatiGenerali", documentData),
            new XElement("DatiBeniServizi", lines, summaries),
            payment.Data);

        var root = new XElement(FatturaPaNamespace + "FatturaElettronica",
            new XAttribute("versione", "FPR12"),
            new XAttribute(XNamespace.Xmlns + "ds", DsNamespace),
            new XAttribute(XNamespace.Xmlns + "p", FatturaPaNamespace),
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
            new XAttribute(XsiNamespace + "schemaLocation", SchemaLocation),
            header,
            body);

        return OperationResult<string>.Ok(Serialize(new XDocument(new XDeclaration("1.0", "UTF-8", null), root)));
    }

    private static XElement? BuildLine(
        InvoicePdfLineModel line, int number, ItalianAccountingCalculator calculator,
        string? requiredZeroRatedNature, List<string> errors)
    {
        var description = Sanitize(line.Description);
        if (string.IsNullOrWhiteSpace(description) || description.Length > 1000)
        {
            errors.Add($"Riga {number}: la descrizione è obbligatoria e al massimo di 1000 caratteri.");
            return null;
        }
        if (!ValidateVat(line.VatRate, line.NatureCode, requiredZeroRatedNature, $"Riga {number}", errors))
            return null;

        var element = new XElement("DettaglioLinee", new XElement("NumeroLinea", number.ToString(Invariant)));
        var sku = Sanitize(line.Sku);
        if (!string.IsNullOrWhiteSpace(sku) && sku.Length <= 35)
            element.Add(new XElement("CodiceArticolo",
                new XElement("CodiceTipo", "SKU"),
                new XElement("CodiceValore", sku)));

        element.Add(
            new XElement("Descrizione", description),
            new XElement("Quantita", Decimals(line.Pieces, 2, 8)),
            new XElement("PrezzoUnitario", Decimals(line.UnitPrice, 2, 8)));

        if (line.DiscountPercent is { } discount && discount > 0m)
        {
            var discountElement = new XElement("ScontoMaggiorazione", new XElement("Tipo", "SC"));
            if (decimal.Round(discount, 2, MidpointRounding.AwayFromZero) == discount)
            {
                discountElement.Add(new XElement("Percentuale", Money(discount)));
            }
            else
            {
                // The effective percentage has more than two decimals: express the discount as a unit amount.
                var discounted = calculator.CalculateDiscountedUnitPrice(line.UnitPrice, discount);
                if (!discounted.Success)
                {
                    errors.Add($"Riga {number}: {discounted.Error}");
                    return null;
                }

                discountElement.Add(new XElement("Importo", Decimals(line.UnitPrice - discounted.Data!.Value, 2, 8)));
            }
            element.Add(discountElement);
        }

        element.Add(
            new XElement("PrezzoTotale", Money(line.LineTotal)),
            new XElement("AliquotaIVA", Money(line.VatRate)));
        if (line.VatRate == 0m)
            element.Add(new XElement("Natura", line.NatureCode));
        return element;
    }

    private static XElement? BuildSummary(
        InvoiceVatBreakdownModel summary, string? requiredZeroRatedNature, List<string> errors)
    {
        if (!ValidateVat(summary.VatRate, summary.ExemptionNature, requiredZeroRatedNature, "Riepilogo IVA", errors))
            return null;

        var element = new XElement("DatiRiepilogo", new XElement("AliquotaIVA", Money(summary.VatRate)));
        if (summary.VatRate == 0m)
            element.Add(new XElement("Natura", summary.ExemptionNature));
        element.Add(
            new XElement("ImponibileImporto", Money(summary.TaxableAmount)),
            new XElement("Imposta", Money(summary.VatAmount)));
        return element;
    }

    private static bool ValidateVat(
        decimal rate, string? nature, string? requiredZeroRatedNature, string context, List<string> errors)
    {
        if (rate is < 0m or > 100m || decimal.Round(rate, 2, MidpointRounding.AwayFromZero) != rate)
        {
            errors.Add($"{context}: l'aliquota IVA deve essere compresa tra 0 e 100 con al massimo 2 decimali.");
            return false;
        }
        if (rate == 0m && (nature is null || !NatureCodes.Contains(nature)))
        {
            errors.Add($"{context}: con aliquota zero serve una Natura IVA valida (N1, N2.1, ... N7).");
            return false;
        }
        if (rate > 0m && nature is not null)
        {
            errors.Add($"{context}: la Natura IVA non è ammessa con aliquota positiva.");
            return false;
        }
        if (rate == 0m && requiredZeroRatedNature is not null && nature != requiredZeroRatedNature)
        {
            errors.Add($"{context}: per un cliente estero l'operazione non imponibile richiede la Natura " +
                       $"{requiredZeroRatedNature} ({(requiredZeroRatedNature == IntraEuNature ? "cessione intracomunitaria" : "esportazione")}), non {nature}.");
            return false;
        }

        return true;
    }

    private static void ValidateParty(
        string role, string? name, string? address, string? city, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(Sanitize(name)))
            errors.Add($"La denominazione del {role} è obbligatoria.");
        if (string.IsNullOrWhiteSpace(Sanitize(address)))
            errors.Add($"L'indirizzo del {role} è obbligatorio.");
        if (string.IsNullOrWhiteSpace(Sanitize(city)))
            errors.Add($"Il comune del {role} è obbligatorio.");
    }

    private static void ValidateItalianPostalCode(string role, string? postalCode, List<string> errors)
    {
        if (postalCode is null || !PostalCodeRegex().IsMatch(postalCode.Trim()))
            errors.Add($"Il CAP del {role} deve essere composto da 5 cifre.");
    }

    /// <summary>
    /// Interprets the customer according to its country: Italian customers are validated strictly, foreign ones
    /// (EU and non-EU) get the foreign identification, the XXXXXXX recipient code and an international postal code.
    /// </summary>
    private static CustomerParty ResolveCustomer(InvoicePdfCustomerDetails customer, List<string> errors)
    {
        var country = string.IsNullOrWhiteSpace(customer.CountryCode) ? "IT" : customer.CountryCode.Trim().ToUpperInvariant();
        ValidateParty("cessionario", customer.BusinessName, customer.Address, customer.City, errors);
        if (!CountryRegex().IsMatch(country))
        {
            errors.Add("Il paese del cessionario deve essere un codice ISO 3166-1 alpha-2 (es. IT, DE, CH).");
            return CustomerParty.Invalid;
        }

        return country == "IT" ? ResolveItalianCustomer(customer, errors) : ResolveForeignCustomer(customer, country, errors);
    }

    private static CustomerParty ResolveItalianCustomer(InvoicePdfCustomerDetails customer, List<string> errors)
    {
        (string Country, string Code)? taxId = null;
        if (!string.IsNullOrWhiteSpace(customer.VatNumber))
        {
            var vat = StripVatCountryPrefix(NormalizeTaxId(customer.VatNumber)!, "IT");
            if (vat is null || !ItalianVatRegex().IsMatch(vat))
                errors.Add("La partita IVA del cessionario italiano deve essere composta da 11 cifre.");
            else
                taxId = ("IT", vat);
        }

        var fiscalCode = NormalizeTaxId(customer.FiscalCode);
        if (fiscalCode is not null && !FiscalCodeRegex().IsMatch(fiscalCode))
        {
            errors.Add("Il codice fiscale del cessionario deve essere composto da 11-16 caratteri alfanumerici.");
            fiscalCode = null;
        }
        if (taxId is null && fiscalCode is null && string.IsNullOrWhiteSpace(customer.VatNumber))
            errors.Add("Il cessionario richiede partita IVA o codice fiscale.");

        ValidateItalianPostalCode("cessionario", customer.PostalCode, errors);
        var recipientCode = NormalizeTaxId(customer.SdiCode);
        if (recipientCode is not null && !RecipientCodeRegex().IsMatch(recipientCode))
            errors.Add("Il codice destinatario del cessionario deve essere composto da 7 caratteri alfanumerici.");

        return new CustomerParty(
            "IT", taxId, fiscalCode, customer.PostalCode?.Trim() ?? string.Empty, Sanitize(customer.City),
            customer.Province, FatturaPaXmlMapper.BuildRecipientAddressElements(recipientCode, customer.Pec),
            RequiredZeroRatedNature: null);
    }

    private static CustomerParty ResolveForeignCustomer(
        InvoicePdfCustomerDetails customer, string country, List<string> errors)
    {
        var isEuropeanUnion = EuropeanUnionCountries.Contains(country);

        // Foreign tax identifier: the community VAT number, otherwise the foreign tax code.
        var vat = NormalizeTaxId(customer.VatNumber);
        var foreignFiscalCode = NormalizeTaxId(customer.FiscalCode);
        var identifier = vat is null ? foreignFiscalCode : isEuropeanUnion ? StripVatCountryPrefix(vat, country) : vat;
        if (vat is not null && identifier is null)
            errors.Add($"La partita IVA del cessionario non appartiene al paese {country}.");
        else if (identifier is null)
            errors.Add($"Il cessionario estero ({country}) richiede partita IVA comunitaria o identificativo fiscale estero.");
        else if (!ForeignTaxIdRegex().IsMatch(identifier))
            errors.Add("L'identificativo fiscale estero del cessionario deve avere da 1 a 28 caratteri alfanumerici.");

        var postalCode = ResolveForeignPostalCode(customer.PostalCode, errors, out var realPostalCode);
        var city = Sanitize(customer.City);
        if (realPostalCode is not null)
        {
            city = $"{city} {realPostalCode}".Trim();
            if (city.Length > 60)
                errors.Add("Il comune del cessionario e il codice postale estero superano 60 caratteri complessivi.");
        }

        // A separate CodiceFiscale is only written when it differs from the identifier and fits the schema.
        var registryFiscalCode = foreignFiscalCode is not null && foreignFiscalCode != identifier &&
                                 FiscalCodeRegex().IsMatch(foreignFiscalCode)
            ? foreignFiscalCode
            : null;
        return new CustomerParty(
            country, identifier is null ? null : (country, identifier), registryFiscalCode, postalCode, city,
            Province: null, [new XElement("CodiceDestinatario", ForeignRecipientCode)],
            isEuropeanUnion ? IntraEuNature : ExtraEuNature);
    }

    // The official schema only accepts five digits as CAP: other international codes are written in the
    // municipality so that no data is lost, and the schema placeholder 99999 is used in the CAP element.
    private static string ResolveForeignPostalCode(string? postalCode, List<string> errors, out string? realPostalCode)
    {
        realPostalCode = null;
        var value = postalCode is null ? string.Empty : WhitespaceRegex().Replace(postalCode.Trim().ToUpperInvariant(), " ");
        if (value.Length == 0)
            return NoPostalCode;
        if (!InternationalPostalCodeRegex().IsMatch(value))
        {
            errors.Add("Il codice postale estero del cessionario deve avere al massimo 12 caratteri alfanumerici.");
            return NoPostalCode;
        }
        if (PostalCodeRegex().IsMatch(value))
            return value;

        realPostalCode = value;
        return NoPostalCode;
    }

    private static string? NormalizeTaxId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WhitespaceRegex().Replace(value.Trim().ToUpperInvariant(), string.Empty);

    // Returns null when the number carries the prefix of another country (Greece uses EL for the VAT).
    private static string? StripVatCountryPrefix(string vat, string country)
    {
        var prefix = country == "GR" ? "EL" : country;
        if (vat.StartsWith(prefix, StringComparison.Ordinal))
            return vat[prefix.Length..];
        if (country == "GR" && vat.StartsWith("GR", StringComparison.Ordinal))
            return vat[2..];

        return vat.Length > 2 && char.IsLetter(vat[0]) && char.IsLetter(vat[1]) ? null : vat;
    }

    private static XElement BuildSeller(InvoicePdfSellerDetails seller, (string Country, string Code) vat, string regime)
    {
        var registry = new XElement("DatiAnagrafici", BuildIdFiscale("IdFiscaleIVA", vat));
        if (!string.IsNullOrWhiteSpace(seller.FiscalCode))
            registry.Add(new XElement("CodiceFiscale", seller.FiscalCode.Trim().ToUpperInvariant()));
        registry.Add(
            new XElement("Anagrafica", new XElement("Denominazione", Sanitize(seller.BusinessName))),
            new XElement("RegimeFiscale", regime));

        var element = new XElement("CedentePrestatore", registry, BuildAddress(
            seller.Address, seller.PostalCode.Trim(), Sanitize(seller.City), seller.Province, "IT"));
        var rea = BuildRea(seller);
        if (rea is not null)
            element.Add(rea);
        return element;
    }

    private static XElement? BuildRea(InvoicePdfSellerDetails seller)
    {
        var office = seller.ReaOffice?.Trim().ToUpperInvariant();
        if (office is null || !ReaOfficeRegex().IsMatch(office) || string.IsNullOrWhiteSpace(seller.Rea))
            return null;

        var number = seller.Rea.Trim();
        if (number.StartsWith(office + "-", StringComparison.OrdinalIgnoreCase))
            number = number[(office.Length + 1)..];
        if (!ReaNumberRegex().IsMatch(number))
            return null;

        var element = new XElement("IscrizioneREA",
            new XElement("Ufficio", office),
            new XElement("NumeroREA", number));
        if (decimal.TryParse(seller.ShareCapital, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out var capital))
            element.Add(new XElement("CapitaleSociale", Money(capital)));
        if (seller.IsSoleShareholder is { } soleShareholder)
            element.Add(new XElement("SocioUnico", soleShareholder ? "SU" : "SM"));
        element.Add(new XElement("StatoLiquidazione", seller.IsInLiquidation == true ? "LS" : "LN"));
        return element;
    }

    private static XElement BuildCustomer(InvoicePdfCustomerDetails customer, CustomerParty party)
    {
        var registry = new XElement("DatiAnagrafici");
        if (party.TaxId is { } taxId)
            registry.Add(BuildIdFiscale("IdFiscaleIVA", taxId));
        if (party.FiscalCode is not null)
            registry.Add(new XElement("CodiceFiscale", party.FiscalCode));
        registry.Add(new XElement("Anagrafica", new XElement("Denominazione", Sanitize(customer.BusinessName))));

        return new XElement("CessionarioCommittente", registry, BuildAddress(
            customer.Address, party.PostalCode, party.City, party.Province, party.Country));
    }

    private static XElement BuildAddress(string street, string postalCode, string city, string? province, string country)
    {
        var element = new XElement("Sede",
            new XElement("Indirizzo", Sanitize(street)),
            new XElement("CAP", postalCode.Trim()),
            new XElement("Comune", city));
        if (province is not null && ProvinceRegex().IsMatch(province.Trim()))
            element.Add(new XElement("Provincia", province.Trim().ToUpperInvariant()));
        element.Add(new XElement("Nazione", country));
        return element;
    }

    private static XElement BuildIdFiscale(string name, (string Country, string Code) id) =>
        new(name, new XElement("IdPaese", id.Country), new XElement("IdCodice", id.Code));

    private static (string Country, string Code)? ParseVatId(string? vatNumber)
    {
        var value = vatNumber?.Replace(" ", string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(value))
            return null;

        var hasCountryPrefix = value.Length > 2 && char.IsLetter(value[0]) && char.IsLetter(value[1]);
        var country = hasCountryPrefix ? value[..2] : "IT";
        var code = hasCountryPrefix ? value[2..] : value;
        if (code.Length is 0 or > 28 || (country == "IT" && !ItalianVatRegex().IsMatch(code)))
            return null;

        return (country, code);
    }

    // The file is transmitted by the seller itself; the progressive must be stable for the same invoice.
    private static string BuildProgressive(string documentNumber)
    {
        var alphanumeric = new string(documentNumber.Where(char.IsAsciiLetterOrDigit).ToArray());
        return alphanumeric.Length <= 10 ? alphanumeric : alphanumeric[^10..];
    }

    private static string? NormalizeIban(string? iban) =>
        string.IsNullOrWhiteSpace(iban) ? null : iban.Replace(" ", string.Empty).ToUpperInvariant();

    private static string Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", Invariant);

    private static string Decimals(decimal value, int minDecimals, int maxDecimals)
    {
        var text = value.ToString("0." + new string('0', maxDecimals), Invariant);
        var trimmed = text.TrimEnd('0');
        var decimals = trimmed.Length - trimmed.IndexOf('.') - 1;
        return decimals >= minDecimals ? trimmed : text[..(text.IndexOf('.') + 1 + minDecimals)];
    }

    // The FatturaPA schema only accepts Basic Latin and Latin-1 characters.
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            builder.Append(character switch
            {
                '\u2018' or '\u2019' => '\'',
                '\u201C' or '\u201D' => '"',
                '\u2013' or '\u2014' => '-',
                '\u20AC' => 'E',
                _ when char.IsControl(character) => ' ',
                _ when character > '\u00FF' => '?',
                _ => character
            });
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static IEnumerable<string> Chunk(string value, int size)
    {
        for (var start = 0; start < value.Length; start += size)
            yield return value.Substring(start, Math.Min(size, value.Length - start));
    }

    private static string Serialize(XDocument document)
    {
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings
               {
                   Indent = true,
                   Encoding = Encoding.UTF8,
                   OmitXmlDeclaration = true
               }))
        {
            document.Root!.WriteTo(writer);
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine + builder;
    }

    [GeneratedRegex(@"^TD\d{2}$")]
    private static partial Regex DocumentTypeRegex();

    [GeneratedRegex(@"^RF\d{2}")]
    private static partial Regex RegimeRegex();

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCodeRegex();

    [GeneratedRegex(@"^[A-Z]{2}$")]
    private static partial Regex CountryRegex();

    [GeneratedRegex(@"^[A-Z0-9]{11,16}$")]
    private static partial Regex FiscalCodeRegex();

    [GeneratedRegex(@"^[A-Z0-9]{7}$")]
    private static partial Regex RecipientCodeRegex();

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.\-/]{0,27}$")]
    private static partial Regex ForeignTaxIdRegex();

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9 \-]{0,11}$")]
    private static partial Regex InternationalPostalCodeRegex();

    [GeneratedRegex(@"^[A-Z]{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex ProvinceRegex();

    [GeneratedRegex(@"^[A-Z]{2}$")]
    private static partial Regex ReaOfficeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{1,20}$")]
    private static partial Regex ReaNumberRegex();

    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex ItalianVatRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
