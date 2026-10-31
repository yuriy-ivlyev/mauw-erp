using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Application;

public class FatturaPaInvoiceXmlBuilderTests
{
    private static readonly ItalianAccountingCalculator Calculator = new();

    [Fact]
    public void MixedRateInvoiceMatchesOfficialSchemaAndKeepsSummaryGroupedByRateAndNature()
    {
        var model = Model(
            [Line("Maglia", 2m, 10m, 22m, null), Line("Libro", 1m, 5m, 0m, "N3.2")],
            [new(22m, 20m, 4.4m, null), new(0m, 5m, 0m, "N3.2")],
            grossTotal: 29.4m);

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(ValidateAgainstOfficialSchema(result.Data!));
        var document = XDocument.Parse(result.Data!);
        Assert.Equal("29.40", document.Descendants("ImportoTotaleDocumento").Single().Value);
        Assert.Equal("TD01", document.Descendants("TipoDocumento").Single().Value);
        Assert.Equal("FPR12", document.Root!.Attribute("versione")!.Value);
        var summaries = document.Descendants("DatiRiepilogo").ToList();
        Assert.Equal(2, summaries.Count);
        Assert.Equal("22.00", summaries[0].Element("AliquotaIVA")!.Value);
        Assert.Null(summaries[0].Element("Natura"));
        Assert.Equal("20.00", summaries[0].Element("ImponibileImporto")!.Value);
        Assert.Equal("4.40", summaries[0].Element("Imposta")!.Value);
        Assert.Equal("0.00", summaries[1].Element("AliquotaIVA")!.Value);
        Assert.Equal("N3.2", summaries[1].Element("Natura")!.Value);
        var lines = document.Descendants("DettaglioLinee").ToList();
        Assert.Equal("N3.2", lines[1].Element("Natura")!.Value);
        Assert.Null(lines[0].Element("Natura"));
    }

    [Fact]
    public void RegenerationIsDeterministicAndProgressiveIsStableForTheSameInvoiceNumber()
    {
        var model = Model([Line("Maglia", 1m, 10m, 22m, null)], [new(22m, 10m, 2.2m, null)], grossTotal: 12.2m);

        var first = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);
        var second = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(first.Success, first.Error);
        Assert.Equal(first.Data, second.Data);
        var progressive = XDocument.Parse(first.Data!).Descendants("ProgressivoInvio").Single().Value;
        Assert.Equal("0261000001", progressive);
        Assert.InRange(progressive.Length, 1, 10);
    }

    [Fact]
    public void FractionalPercentDiscountIsExpressedAsUnitAmountAndStillValidatesAgainstSchema()
    {
        var line = Line("Maglia", 1m, 100m, 22m, null) with
        {
            DiscountPercent = 12.4575m,
            LineTotal = 87.54m
        };
        var model = Model([line], [new(22m, 87.54m, 19.26m, null)], grossTotal: 106.80m);

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(ValidateAgainstOfficialSchema(result.Data!));
        var discount = XDocument.Parse(result.Data!).Descendants("ScontoMaggiorazione").Single();
        Assert.Equal("SC", discount.Element("Tipo")!.Value);
        Assert.Null(discount.Element("Percentuale"));
        Assert.Equal("12.4575", discount.Element("Importo")!.Value);
    }

    [Fact]
    public void UnitPriceKeepsUpToEightDecimalsAndQuantityStaysFractional()
    {
        var line = Line("Tessuto", 1.5m, 0.12345678m, 22m, null) with { LineTotal = 0.19m };
        var model = Model([line], [new(22m, 0.19m, 0.04m, null)], grossTotal: 0.23m);

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        var detail = XDocument.Parse(result.Data!).Descendants("DettaglioLinee").Single();
        Assert.Equal("0.12345678", detail.Element("PrezzoUnitario")!.Value);
        Assert.Equal("1.50", detail.Element("Quantita")!.Value);
        Assert.Equal("0.19", detail.Element("PrezzoTotale")!.Value);
    }

    [Fact]
    public void ZeroRatedLineWithoutNatureIsRejectedInsteadOfGuessingOne()
    {
        var model = Model([Line("Libro", 1m, 5m, 0m, null)], [new(0m, 5m, 0m, null)], grossTotal: 5m);

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.False(result.Success);
        Assert.Contains("Natura", result.Error);
    }

    [Fact]
    public void PositiveRateWithNatureIsRejected()
    {
        var model = Model([Line("Maglia", 1m, 10m, 22m, "N1")], [new(22m, 10m, 2.2m, "N1")], grossTotal: 12.2m);

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.False(result.Success);
        Assert.Contains("Natura", result.Error);
    }

    [Fact]
    public void MissingMandatoryPartyDataReportsEveryProblem()
    {
        var model = Model([Line("Maglia", 1m, 10m, 22m, null)], [new(22m, 10m, 2.2m, null)], grossTotal: 12.2m) with
        {
            Seller = Seller() with { VatNumber = string.Empty, PostalCode = "123" },
            Customer = Customer() with { VatNumber = null, FiscalCode = null, City = " " }
        };

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.False(result.Success);
        Assert.Contains("partita IVA del cedente", result.Error);
        Assert.Contains("CAP del cedente", result.Error);
        Assert.Contains("partita IVA o codice fiscale", result.Error);
        Assert.Contains("comune del cessionario", result.Error);
    }

    [Fact]
    public void ItalianCustomerRulesStayStrict()
    {
        Assert.Contains("11 cifre", Build(Customer() with { VatNumber = "1234567890" }).Error);
        Assert.Contains("11 cifre", Build(Customer() with { VatNumber = "IT1234567890A" }).Error);
        Assert.Contains("11 cifre", Build(Customer() with { VatNumber = "DE123456789" }).Error);
        Assert.Contains("CAP del cessionario", Build(Customer() with { PostalCode = "2010" }).Error);
        Assert.Contains("CAP del cessionario", Build(Customer() with { PostalCode = "2010A" }).Error);
        Assert.Contains("7 caratteri", Build(Customer() with { SdiCode = "ABC123" }).Error);
        Assert.Contains("ISO 3166-1", Build(Customer() with { CountryCode = "Italia" }).Error);
    }

    [Fact]
    public void ItalianCustomerKeepsItalianIdentificationAndNormalizesCaseAndSdiCode()
    {
        var result = Build(Customer() with { CountryCode = "it", SdiCode = " ns9q9ed ", Pec = "x@pec.test" });

        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        var document = XDocument.Parse(result.Data!);
        var customer = document.Descendants("CessionarioCommittente").Single();
        Assert.Equal("IT", customer.Descendants("IdPaese").Single().Value);
        Assert.Equal("09876543210", customer.Descendants("IdCodice").Single().Value);
        Assert.Equal("20100", customer.Descendants("CAP").Single().Value);
        Assert.Equal("MI", customer.Descendants("Provincia").Single().Value);
        Assert.Equal("IT", customer.Descendants("Nazione").Single().Value);
        Assert.Equal("NS9Q9ED", document.Descendants("CodiceDestinatario").Single().Value);
        Assert.Empty(document.Descendants("PECDestinatario"));
    }

    [Fact]
    public void GermanCustomerWithCommunityVatMatchesOfficialSchemaAndUsesIntraEuNature()
    {
        var model = ForeignModel(
            Customer() with
            {
                CountryCode = "de", VatNumber = "DE 123 456 789", Address = "Friedrichstrasse 12", City = "Berlin",
                PostalCode = "10115", Province = "BE", SdiCode = "KRRH6B9", Pec = "kunde@example.test"
            },
            nature: "N3.2");

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        var document = XDocument.Parse(result.Data!);
        var customer = document.Descendants("CessionarioCommittente").Single();
        Assert.Equal("DE", customer.Descendants("IdPaese").Single().Value);
        Assert.Equal("123456789", customer.Descendants("IdCodice").Single().Value);
        Assert.Equal("10115", customer.Descendants("CAP").Single().Value);
        Assert.Equal("Berlin", customer.Descendants("Comune").Single().Value);
        Assert.Empty(customer.Descendants("Provincia"));
        Assert.Equal("DE", customer.Descendants("Nazione").Single().Value);
        Assert.Equal("XXXXXXX", document.Descendants("CodiceDestinatario").Single().Value);
        Assert.Empty(document.Descendants("PECDestinatario"));
        Assert.Equal("N3.2", document.Descendants("DettaglioLinee").Single().Element("Natura")!.Value);
        Assert.Equal("N3.2", document.Descendants("DatiRiepilogo").Single().Element("Natura")!.Value);
        var seller = document.Descendants("CedentePrestatore").Single();
        Assert.Equal("IT", seller.Descendants("Nazione").Single().Value);
    }

    [Fact]
    public void SwissCustomerWithFourDigitPostalCodeUsesPlaceholderCapAndExportNature()
    {
        var model = ForeignModel(
            Customer() with
            {
                CountryCode = "CH", VatNumber = "CHE-123.456.789", Address = "Bahnhofstrasse 1", City = "Zurigo",
                PostalCode = "8001", Province = "ZH", SdiCode = "ABCDEFG"
            },
            nature: "N3.1");

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        var document = XDocument.Parse(result.Data!);
        var customer = document.Descendants("CessionarioCommittente").Single();
        Assert.Equal("CH", customer.Descendants("IdPaese").Single().Value);
        Assert.Equal("CHE-123.456.789", customer.Descendants("IdCodice").Single().Value);
        Assert.Equal("99999", customer.Descendants("CAP").Single().Value);
        Assert.Equal("Zurigo 8001", customer.Descendants("Comune").Single().Value);
        Assert.Equal("CH", customer.Descendants("Nazione").Single().Value);
        Assert.Equal("XXXXXXX", document.Descendants("CodiceDestinatario").Single().Value);
        Assert.All(document.Descendants("Natura"), natura => Assert.Equal("N3.1", natura.Value));
        Assert.Equal(2, document.Descendants("Natura").Count());
    }

    [Fact]
    public void UsCustomerWithoutVatUsesTheForeignTaxCodeAndPlaceholderWhenNoPostalCodeExists()
    {
        var model = ForeignModel(
            Customer() with
            {
                CountryCode = "US", VatNumber = null, FiscalCode = "12-3456789", Address = "5th Avenue 1",
                City = "New York", PostalCode = " ", Province = "NY"
            },
            nature: "N3.1");

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        var customer = XDocument.Parse(result.Data!).Descendants("CessionarioCommittente").Single();
        Assert.Equal("US", customer.Descendants("IdPaese").Single().Value);
        Assert.Equal("12-3456789", customer.Descendants("IdCodice").Single().Value);
        Assert.Empty(customer.Descendants("CodiceFiscale"));
        Assert.Equal("99999", customer.Descendants("CAP").Single().Value);
        Assert.Equal("New York", customer.Descendants("Comune").Single().Value);
    }

    [Fact]
    public void BritishAlphanumericPostalCodeIsPreservedInTheMunicipality()
    {
        var model = ForeignModel(
            Customer() with
            {
                CountryCode = "GB", VatNumber = "GB123456789", Address = "Baker Street 221B", City = "London",
                PostalCode = "sw1a  1aa"
            },
            nature: "N3.1");

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        var customer = XDocument.Parse(result.Data!).Descendants("CessionarioCommittente").Single();
        Assert.Equal("99999", customer.Descendants("CAP").Single().Value);
        Assert.Equal("London SW1A 1AA", customer.Descendants("Comune").Single().Value);
        Assert.Equal("GB123456789", customer.Descendants("IdCodice").Single().Value);
    }

    [Fact]
    public void GreekVatUsesTheElPrefixAndKeepsTheIsoCountryAsIdPaese()
    {
        var model = ForeignModel(
            Customer() with { CountryCode = "GR", VatNumber = "EL123456789", PostalCode = "10431" }, "N3.2");

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        var customer = XDocument.Parse(result.Data!).Descendants("CessionarioCommittente").Single();
        Assert.Equal("GR", customer.Descendants("IdPaese").Single().Value);
        Assert.Equal("123456789", customer.Descendants("IdCodice").Single().Value);
    }

    [Fact]
    public void ForeignCustomersWithPositiveRatesNeedNoNatureAndStillGetTheForeignRecipientCode()
    {
        var model = Model([Line("Maglia", 1m, 10m, 22m, null)], [new(22m, 10m, 2.2m, null)], grossTotal: 12.2m) with
        {
            Customer = Customer() with { CountryCode = "FR", VatNumber = "FR12345678901", PostalCode = "75001" }
        };

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        Assert.Equal("XXXXXXX", XDocument.Parse(result.Data!).Descendants("CodiceDestinatario").Single().Value);
    }

    [Theory]
    [InlineData("DE", "N3.1", "N3.2", "intracomunitaria")]
    [InlineData("ES", "N2.1", "N3.2", "intracomunitaria")]
    [InlineData("CH", "N3.2", "N3.1", "esportazione")]
    [InlineData("US", "N7", "N3.1", "esportazione")]
    public void ZeroRatedForeignOperationsMustCarryTheNatureOfTheirArea(
        string country, string wrongNature, string expected, string description)
    {
        var model = ForeignModel(
            Customer() with { CountryCode = country, VatNumber = country == "US" ? null : country + "123456789", FiscalCode = country == "US" ? "123456789" : null },
            wrongNature);

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.False(result.Success);
        Assert.Contains($"richiede la Natura {expected}", result.Error);
        Assert.Contains(description, result.Error);
        Assert.Contains("Riga 1", result.Error);
        Assert.Contains("Riepilogo IVA", result.Error);
    }

    [Fact]
    public void ForeignCustomerProblemsAreReportedExplicitly()
    {
        Assert.Contains("richiede partita IVA comunitaria o identificativo fiscale estero",
            Build(Customer() with { CountryCode = "DE", VatNumber = null, FiscalCode = null, PostalCode = "10115" }).Error);
        Assert.Contains("non appartiene al paese DE",
            Build(Customer() with { CountryCode = "DE", VatNumber = "FR12345678901", PostalCode = "10115" }).Error);
        Assert.Contains("al massimo 12 caratteri",
            Build(Customer() with { CountryCode = "DE", VatNumber = "DE123456789", PostalCode = "1234567890123" }).Error);
        Assert.Contains("al massimo 12 caratteri",
            Build(Customer() with { CountryCode = "DE", VatNumber = "DE123456789", PostalCode = "10115*" }).Error);
        Assert.Contains("da 1 a 28 caratteri",
            Build(Customer() with { CountryCode = "CH", VatNumber = new string('1', 29), PostalCode = "8001" }).Error);
        Assert.Contains("60 caratteri",
            Build(Customer() with { CountryCode = "GB", VatNumber = "GB123456789", City = new string('L', 55), PostalCode = "SW1A 1AA" }).Error);
    }

    [Fact]
    public void TwelveCharacterInternationalPostalCodeIsAccepted()
    {
        var result = Build(Customer() with { CountryCode = "CA", VatNumber = "123456789RT0001", PostalCode = "K1A 0B1-1234" });

        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
    }

    private static OperationResult<string> Build(InvoicePdfCustomerDetails customer) =>
        FatturaPaInvoiceXmlBuilder.Build(
            Model([Line("Maglia", 1m, 10m, 22m, null)], [new(22m, 10m, 2.2m, null)], grossTotal: 12.2m) with
            {
                Customer = customer
            }, Calculator);

    private static InvoicePdfModel ForeignModel(InvoicePdfCustomerDetails customer, string nature) =>
        Model([Line("Maglia", 2m, 10m, 0m, nature)], [new(0m, 20m, 0m, nature)], grossTotal: 20m) with
        {
            Customer = customer
        };

    [Fact]
    public void TextOutsideLatin1IsSanitizedSoSchemaStaysValid()
    {
        var model = Model([Line("Maglia “speciale” – blu €", 1m, 10m, 22m, null)],
            [new(22m, 10m, 2.2m, null)], grossTotal: 12.2m);

        var result = FatturaPaInvoiceXmlBuilder.Build(model, Calculator);

        Assert.True(result.Success, result.Error);
        Assert.Empty(ValidateAgainstOfficialSchema(result.Data!));
        Assert.Equal("Maglia \"speciale\" - blu E",
            XDocument.Parse(result.Data!).Descendants("Descrizione").Single().Value);
    }

    [Fact]
    public void SchemaValidationDetectsBrokenDocuments()
    {
        var model = Model([Line("Maglia", 1m, 10m, 22m, null)], [new(22m, 10m, 2.2m, null)], grossTotal: 12.2m);
        var document = XDocument.Parse(FatturaPaInvoiceXmlBuilder.Build(model, Calculator).Data!);
        document.Descendants("ImponibileImporto").Single().Remove();

        Assert.NotEmpty(ValidateAgainstOfficialSchema(document.ToString()));
    }

    private static InvoicePdfModel Model(
        List<InvoicePdfLineModel> lines, List<InvoiceVatBreakdownModel> summary, decimal grossTotal) => new(
        DocumentNumber: "FAT-202610-00001",
        DocumentType: "TD01",
        IssueDate: new DateOnly(2026, 10, 31),
        Currency: "EUR",
        Seller: Seller(),
        Customer: Customer(),
        Payment: new InvoicePdfPaymentInfo("A vista", "MP01", new DateOnly(2026, 10, 31), "IT60X0542811101000000123456",
            "BPITITMM", "Banca Prova", grossTotal),
        Lines: lines,
        VatSummary: summary,
        Totals: new InvoicePdfTotals(lines.Sum(line => line.Pieces), summary.Sum(row => row.TaxableAmount),
            summary.Sum(row => row.VatAmount), grossTotal),
        FiscalNotes: "Merce viaggiante con imballo originale.");

    private static InvoicePdfSellerDetails Seller() => new(
        "Mauw S.r.l.", "IT01234567890", "01234567890", "Via Roma 1", "Bologna", "40100", "BO",
        "BO-591450", "10.000,00", "RF01 - Ordinario", "KRRH6B9", "mauw@pec.example.test",
        IsShareCapitalFullyPaid: true, ReaOffice: "BO", IsSoleShareholder: true, IsInLiquidation: false);

    private static InvoicePdfCustomerDetails Customer() => new(
        "Cliente Prova S.r.l.", "IT09876543210", null, "Via Milano 2", "Milano", "20100", "MI", "0000000",
        "cliente@pec.example.test");

    private static InvoicePdfLineModel Line(
        string description, decimal pieces, decimal unitPrice, decimal vatRate, string? nature) => new(
        Sku: "SKU-1", Description: description, SeasonCollection: null, Pieces: pieces, UnitPrice: unitPrice,
        DiscountPercent: null, VatRate: vatRate, LineTotal: decimal.Round(pieces * unitPrice, 2, MidpointRounding.AwayFromZero),
        NatureCode: nature);

    private static List<string> ValidateAgainstOfficialSchema(string xml) => FatturaPaSchemaValidator.Validate(xml);
}
