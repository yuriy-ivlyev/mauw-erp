using System.Text;
using System.Globalization;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Infrastructure.Reporting;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Infrastructure.Branding;
using Moq;

namespace Mauw.Erp.UnitTests.Reporting;

public class InvoicePdfGeneratorTests
{
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        var logos = new Mock<ICompanyLogoService>();
        logos.Setup(service => service.GetAsync("compact", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<CompanyLogoDto>.Ok(new(DefaultCompanyLogos.ReadPng("compact"))));
        services.AddSingleton(logos.Object);
        return services;
    }

    [Theory]
    [InlineData("ORDINE")]
    [InlineData("TD01")]
    [InlineData("DDT")]
    [InlineData("NOTA CREDITO")]
    public async Task GeneratesReadablePdfUsingEmbeddedFonts(string documentType)
    {
        var services = CreateServices();
        services.AddInvoicePdfReporting();
        // Repeated registration must not reset PDFsharp's process-wide font settings.
        services.AddInvoicePdfReporting();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<IInvoicePdfGenerator>();
        var model = new InvoicePdfModel(
            "DOC-001", documentType, new DateOnly(2026, 10, 18), "EUR",
            new("Venditore", "00000000000", null, "Via Roma 1", "Roma", "00100", "RM",
                null, null, "RF01", null, null),
            new("Cliente", null, null, "Via Milano 2", "Roma", "00100", "RM", null, null),
            new("Pagamento immediato", "MP01", null, null, null, null),
            [new("SKU-1", "Caff\u00e8 e qualit\u00e0", null, 1, 100m, null, 22m, 100m)],
            [new(22m, 100m, 22m)],
            new(1, 100m, 22m, 122m));

        var bytes = await generator.GenerateInvoicePdfAsync(model);

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, 8));
        using var stream = new MemoryStream(bytes);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.True(document.PageCount > 0);
        using var readablePdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var firstPage = readablePdf.GetPage(1);
        var logo = Assert.Single(firstPage.GetImages());
        Assert.InRange(logo.BoundingBox.Width, 1, 35);
        Assert.InRange(logo.BoundingBox.Height, 1, 39);
        var seller = firstPage.GetWords().Where(word => word.Text == "Venditore")
            .OrderByDescending(word => word.BoundingBox.Top).First();
        Assert.InRange(Math.Abs(seller.BoundingBox.Top - logo.BoundingBox.Top), 0, 6);
        Assert.True(seller.BoundingBox.Left > logo.BoundingBox.Right);
        foreach (var word in firstPage.GetWords())
        {
            var overlapX = Math.Min(word.BoundingBox.Right, logo.BoundingBox.Right) -
                Math.Max(word.BoundingBox.Left, logo.BoundingBox.Left);
            var overlapY = Math.Min(word.BoundingBox.Top, logo.BoundingBox.Top) -
                Math.Max(word.BoundingBox.Bottom, logo.BoundingBox.Bottom);
            Assert.False(overlapX > 1 && overlapY > 1, $"Logo overlaps '{word.Text}'.");
        }
        var customerHeading = Assert.Single(firstPage.GetWords(), word => word.Text == "CESSIONARIO");
        Assert.InRange(firstPage.Height - customerHeading.BoundingBox.Top, 0, 160);
        if (documentType == "ORDINE")
            Assert.Equal("Ordine DOC-001", document.Info.Title);
        var resolver = Assert.IsAssignableFrom<IFontResolver>(GlobalFontSettings.FontResolver);
        foreach (var (bold, italic) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            var font = Assert.IsType<FontResolverInfo>(resolver.ResolveTypeface("Arial", bold, italic));
            Assert.NotEmpty(Assert.IsType<byte[]>(resolver.GetFont(font.FaceName)));
        }

        Assert.NotNull(resolver.ResolveTypeface("Courier New", false, false));
    }

    [Fact]
    public async Task RendersSellerLegalDetailsCourtesyNoticeAndPaymentSummary()
    {
        var services = CreateServices();
        services.AddInvoicePdfReporting();
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IInvoicePdfGenerator>();
        var model = new InvoicePdfModel(
            "F-2026-001", "TD01", new DateOnly(2026, 10, 25), "EUR",
            new("MAUW S.R.L.", "01234567890", "01234567890", "Via Roma 1", "Bologna", "40100", "BO",
                "BO-591450", "10.000,00", "RF01", null, null, true,
                "BO", "Società a responsabilità limitata", true, false),
            new("Cliente", null, null, "Via Milano 2", "Roma", "00100", "RM", null, null),
            new("A vista", "MP01", new DateOnly(2026, 10, 25), null, null, null, 122.20m),
            [new("MODEL-1", "Articolo", null, 12, 18.85245902m, null, 22m, 122.20m,
                ModelCode: "MODEL-1", Color: "Navy", Size: "M")],
            [new(22m, 100m, 22m)],
            new(12, 100m, 22m, 122.20m));

        var bytes = await generator.GenerateInvoicePdfAsync(model);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var words = pdf.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text).ToArray();
        var text = string.Join(" ", words);
        Assert.Contains("Società a responsabilità limitata", text);
        Assert.Contains("socio unico", text);
        Assert.Contains("non in liquidazione", text);
        Assert.Contains("BO-591450", text);
        Assert.Contains("BO - REA BO-591450", text);
        Assert.Contains("10.000,00", text);
        Assert.Contains("Copia di cortesia priva di valenza fiscale", text);
        Assert.Contains("21 D.P.R. 633/72", text);
        Assert.Contains("Agenzia delle Entrate.", text);
        Assert.Contains("Data scadenza rata / quietanza", text);
        Assert.Contains("25/10/2026", text);
        Assert.Contains("122,20", text);
        Assert.Contains("MP01 - A vista", text);
        Assert.Contains(words, word => word == "12");
        Assert.DoesNotContain("12,00", words);
        Assert.Contains(words, word => word == "18,85");

        var footerWords = pdf.GetPage(pdf.NumberOfPages).GetWords()
            .Where(word => word.BoundingBox.Top < 65)
            .ToArray();
        var footerCapital = Assert.Single(footerWords, word => word.Text == "Capitale");
        var footerRegistry = Assert.Single(footerWords, word => word.Text == "REA");
        Assert.InRange(Math.Abs(footerCapital.BoundingBox.Top - footerRegistry.BoundingBox.Top), 0, 1);
        Assert.DoesNotContain("Bold", footerCapital.FontName, StringComparison.OrdinalIgnoreCase);
        var headerCapital = Assert.Single(pdf.GetPage(1).GetWords(),
            word => word.Text == "Capitale" && word.BoundingBox.Top > 65);
        Assert.DoesNotContain("Bold", headerCapital.FontName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrdersInvoiceLinesByModelColorAndStandardSize()
    {
        var services = CreateServices();
        services.AddInvoicePdfReporting();
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IInvoicePdfGenerator>();
        var lines = new[]
        {
            new InvoicePdfLineModel("B-1", "MARKER-B", null, 1, 1m, null, 22m, 1m,
                ModelCode: "B", Color: "Black", Size: "M"),
            new InvoicePdfLineModel("A-3", "MARKER-XXL", null, 1, 1m, null, 22m, 1m,
                ModelCode: "A", Color: "Navy", Size: "XXL"),
            new InvoicePdfLineModel("A-2", "MARKER-M", null, 1, 1m, null, 22m, 1m,
                ModelCode: "A", Color: "Navy", Size: "M"),
            new InvoicePdfLineModel("A-1", "MARKER-S", null, 1, 1m, null, 22m, 1m,
                ModelCode: "A", Color: "Black", Size: "S"),
            new InvoicePdfLineModel("A-0", "MARKER-XS", null, 1, 1m, null, 22m, 1m,
                ModelCode: "A", Color: "Navy", Size: "XS")
        };
        var model = new InvoicePdfModel(
            "F-ORDER", "TD01", new DateOnly(2026, 10, 25), "EUR",
            new("MAUW S.R.L.", "01234567890", null, "Via Roma 1", "Bologna", "40100", "BO",
                null, null, "RF01", null, null),
            new("Cliente", null, null, "Via Milano 2", "Roma", "00100", "RM", null, null),
            new("A vista", "MP01", null, null, null, null),
            lines.ToList(), [new(22m, 5m, 1.10m)], new(5, 5m, 1.10m, 6.10m));

        var bytes = await generator.GenerateInvoicePdfAsync(model);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var page = pdf.GetPage(1);
        var markers = page.GetWords()
            .Where(word => word.Text.StartsWith("MARKER-", StringComparison.Ordinal))
            .OrderByDescending(word => word.BoundingBox.Top)
            .Select(word => word.Text)
            .ToArray();

        Assert.Equal(["MARKER-S", "MARKER-XS", "MARKER-M", "MARKER-XXL", "MARKER-B"], markers);
    }

    [Theory]
    [InlineData("ORDINE", 1, false)]
    [InlineData("TD01", 26, false)]
    [InlineData("ORDINE", 80, false)]
    [InlineData("TD01", 26, true)]
    [InlineData("DDT", 1, false)]
    [InlineData("NOTA CREDITO", 1, false)]
    [InlineData("TD01", 0, false)]
    public async Task RendersShortAndMultipageDocuments(string documentType, int lineCount, bool longDetails)
    {
        var services = CreateServices();
        services.AddInvoicePdfReporting();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<IInvoicePdfGenerator>();
        var unitPrice = longDetails ? 123456.78m : 21.74m;
        var lines = Enumerable.Range(1, lineCount)
            .Select(i => new InvoicePdfLineModel($"SKU-{i:D4}",
                $"Articolo RIGA{i:D4} - Modello classico / Blu / Taglia M" +
                (longDetails
                    ? " - Descrizione estesa con dettagli della composizione, della lavorazione e delle condizioni di fornitura del prodotto."
                    : ""),
                "FW 26/27", 1, unitPrice, null, 22m, unitPrice))
            .ToList();
        var net = unitPrice * lineCount;
        var vat = Math.Round(net * 0.22m, 2, MidpointRounding.AwayFromZero);
        var sellerName = longDetails
            ? "AZIENDA DEMO COMMERCIO INTERNAZIONALE ABBIGLIAMENTO E ACCESSORI SOCIETA A RESPONSABILITA LIMITATA"
            : "AZIENDA DEMO SRL";
        var model = new InvoicePdfModel(
            "DOC-2026-001", documentType, new DateOnly(2026, 10, 18), "EUR",
            new(sellerName, "00000000000", null, "Via della Moda 45", "Bologna", "40100", "BO",
                "BO-123456", "10.000,00", "RF01 - Ordinario", null, "amministrazione@example.test"),
            new(longDetails ? $"{sellerName} - FILIALE COMMERCIALE" : "CLIENTE DEMO SRL",
                "00000000001", null,
                longDetails
                    ? "Via del Commercio Internazionale 123, Palazzo Commerciale, Blocco Secondo"
                    : "Via Roma 10",
                "Milano", "20100", "MI", "ABCDEFG", "cliente@example.test"),
            new(longDetails ? "Bonifico bancario a 30/60/90 giorni data fattura fine mese" : "Bonifico 30 giorni",
                "MP05", new DateOnly(2026, 11, 30), "IT00 X000 0000 0000 0000 0000 000", "DEMOIT00", "Banca Demo"),
            lines,
            [new(22m, net, vat), new(0m, 0m, 0m, "N2.1")],
            new(lineCount, net, vat, net + vat),
            new("Agente Demo", "AG-01"),
            "ORD-2026-001", new DateOnly(2026, 10, 1), "DDT-001", "Franco fabbrica",
            "Merce con imballaggio originale e identificativi SKU. Verificare quantita e taglie alla consegna.");

        var previousCulture = CultureInfo.CurrentCulture;
        byte[] bytes;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            bytes = await generator.GenerateInvoicePdfAsync(model);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        using var stream = new MemoryStream(bytes);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        if (lineCount <= 1)
            Assert.Equal(1, document.PageCount);
        else
            Assert.True(document.PageCount >= 2);
        foreach (var page in document.Pages)
        {
            Assert.InRange(page.Width.Point, 595, 596);
            Assert.InRange(page.Height.Point, 841, 843);
        }

        AssertLayout(bytes, lineCount, documentType, net + vat);

        var outputDirectory = Environment.GetEnvironmentVariable("PDF_LAYOUT_OUTPUT");
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllBytesAsync(
                Path.Combine(outputDirectory, $"layout-{documentType}-{lineCount}-{longDetails}.pdf"), bytes);
        }
    }

    [Fact]
    public async Task PdfPreservesScorporoPrecisionNatureAndCalculatedTotals()
    {
        var calculator = new ItalianAccountingCalculator();
        var result = calculator.Calculate([
            new DocumentLineInput(5m, 23m, 22m, PriceIncludesVat: true),
            new DocumentLineInput(1m, 5m, 0m, "N3.2")]);
        Assert.True(result.Success, result.Error);
        var calculated = result.Data!;
        var services = CreateServices();
        services.AddInvoicePdfReporting();
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IInvoicePdfGenerator>();
        var model = new InvoicePdfModel(
            "FISCAL-001", "TD01", new DateOnly(2026, 10, 18), "EUR",
            new("Venditore", "00000000000", null, "Via Roma 1", "Roma", "00100", "RM",
                null, null, "RF01", null, null),
            new("Cliente", null, null, "Via Milano 2", "Roma", "00100", "RM", null, null),
            new("Pagamento immediato", "MP01", null, null, null, null),
            calculated.Lines.Select(line => new InvoicePdfLineModel("SKU", "Articolo", null,
                (int)line.Quantity, line.UnitNetPrice, line.DiscountPercent, line.VatRate,
                line.LineNetTotal, line.NatureCode)).ToList(),
            calculated.VatSummary.Select(group => new InvoiceVatBreakdownModel(
                group.VatRate, group.TaxableAmount, group.VatAmount, group.NatureCode)).ToList(),
            new(6, calculated.TotalTaxable, calculated.TotalVat, calculated.TotalGross));

        var bytes = await generator.GenerateInvoicePdfAsync(model);

        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var words = pdf.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text).ToArray();
        Assert.Contains("18,85", words);
        Assert.DoesNotContain("18,85245902", words);
        Assert.Contains("94,26", words);
        Assert.Contains("20,74", words);
        Assert.Contains("120,00", words);
        Assert.Contains("N3.2", words);
    }

    private static void AssertLayout(byte[] bytes, int lineCount, string documentType, decimal gross)
    {
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var allWords = new List<string>();
        var seenItems = new List<string>();
        for (var pageNumber = 1; pageNumber <= pdf.NumberOfPages; pageNumber++)
        {
            var page = pdf.GetPage(pageNumber);
            var words = page.GetWords().ToArray();
            allWords.AddRange(words.Select(word => word.Text));
            var items = words.Where(word => word.Text.StartsWith("RIGA", StringComparison.Ordinal)).ToArray();
            seenItems.AddRange(items.Select(word => word.Text));
            var headings = words.Where(word => word.Text == "#").ToArray();

            if (items.Length > 0)
            {
                var heading = Assert.Single(headings);
                Assert.All(items, item => Assert.True(item.BoundingBox.Top < heading.BoundingBox.Bottom));
            }

            if (pageNumber == 1)
            {
                var customerHeading = Assert.Single(words, word => word.Text == "CESSIONARIO");
                var tableHeading = Assert.Single(headings);
                Assert.True(tableHeading.BoundingBox.Top < customerHeading.BoundingBox.Bottom);
                Assert.DoesNotContain(words, word => word.Text == "DOC:");
            }
            else
            {
                var continuation = Assert.Single(words, word => word.Text == "DOC:");
                Assert.All(items, item => Assert.True(item.BoundingBox.Top < continuation.BoundingBox.Bottom));
            }

            foreach (var word in words)
            {
                Assert.InRange(word.BoundingBox.Left, 38, 557);
                Assert.InRange(word.BoundingBox.Right, 38, 557);
                Assert.InRange(word.BoundingBox.Bottom, 10, 831);
                Assert.InRange(word.BoundingBox.Top, 10, 831);
            }

            for (var i = 0; i < words.Length; i++)
            {
                for (var j = i + 1; j < words.Length; j++)
                {
                    var a = words[i].BoundingBox;
                    var b = words[j].BoundingBox;
                    var overlapX = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
                    var overlapY = Math.Min(a.Top, b.Top) - Math.Max(a.Bottom, b.Bottom);
                    Assert.False(overlapX > 1 && overlapY > 2,
                        $"Overlapping text on page {pageNumber}: '{words[i].Text}' / '{words[j].Text}'.");
                }
            }
        }

        Assert.Equal(Enumerable.Range(1, lineCount).Select(i => $"RIGA{i:D4}"), seenItems);
        Assert.Single(allWords, word => word == "TOTALE");
        Assert.Contains(gross.ToString("N2", CultureInfo.GetCultureInfo("it-IT")), allWords);
        var lastPageWords = pdf.GetPage(pdf.NumberOfPages).GetWords().ToArray();
        var total = Assert.Single(lastPageWords, word => word.Text == "TOTALE");
        var lastItem = lastPageWords.LastOrDefault(word => word.Text.StartsWith("RIGA", StringComparison.Ordinal));
        if (lastItem is not null)
            Assert.True(total.BoundingBox.Top < lastItem.BoundingBox.Bottom);
        if (documentType is "ORDINE" or "DDT")
            Assert.DoesNotContain(allWords, word => word.Equals("Fattura", StringComparison.OrdinalIgnoreCase));
    }
}