using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;

namespace Mauw.Erp.UnitTests.Reporting;

public sealed class InventoryStockPdfGeneratorTests
{
    [Fact]
    public void Generate_CreatesLandscapePdfWithCompanyRowsAndTotals()
    {
        var services = new ServiceCollection();
        services.AddInventoryStockReporting();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var company = new CompanySettingsDto(
            Guid.NewGuid(),
            "Mauw Test S.r.l.",
            "12345678901",
            null,
            null,
            null,
            "IT",
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            []);
        var item = new InventoryStockDto(
            Guid.NewGuid(),
            "DEP-1",
            "NomeMagazzinoDaNonStampare",
            Guid.NewGuid(),
            "SKU-1",
            "123456",
            "Articolo di prova",
            "Blu",
            "M",
            "Abbigliamento",
            2m,
            0m,
            1m,
            3m,
            "pz",
            10.12345678m,
            20.25m,
            "StockMovements");
        var report = new InventoryStockExportDto(
            [item],
            new InventoryStockTotalsDto(2m, 0m, 1m, 3m, 20.25m),
            DateTime.UtcNow,
            null,
            "Ordini assegnati al deposito predefinito.",
            Mauw.Erp.Contracts.Enums.ValuationMethod.WeightedAverageCost,
            new InventoryStockFilterSummaryDto("camicia", "Deposito", "Abbigliamento", true));
        company = company with
        {
            LegalHeadquarters = new CompanyAddressSiteDto(
                Guid.NewGuid(),
                "LegalHeadquarters",
                null,
                "Via Roma 1",
                "20100",
                "Milano",
                "MI",
                "IT",
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null)
        };

        var generator = scope.ServiceProvider.GetRequiredService<IInventoryStockPdfGenerator>();
        var bytes = generator.Generate(report, company);

        using var pdf = PdfDocument.Open(bytes);
        var page = Assert.Single(pdf.GetPages());
        Assert.True(page.Width > page.Height);
        var text = string.Join(" ", page.GetWords().Select(word => word.Text));
        Assert.Contains("Mauw", text);
        Assert.Contains("Articolo", text);
        Assert.Contains("DEP-1", text);
        Assert.DoesNotContain("NomeMagazzinoDaNonStampare", text);
        Assert.Contains("Costo", text);
        Assert.Contains("medio", text);
        Assert.Contains("ponderato", text);
        Assert.Contains("TOTALI", text);
        Assert.Contains("10,12345678", text);
        Assert.Contains("20,25", text);
        Assert.Contains("Pag. 1 di 1", text);
        Assert.Contains("Sede", text);
        Assert.Contains("legale", text);
        Assert.Contains("Via", text);
        Assert.Contains("Roma", text);
        Assert.Contains("20100", text);
        Assert.Contains("Milano", text);
        Assert.Contains("Filtri", text);
        Assert.Contains("camicia", text);
        Assert.Contains("Deposito", text);
        Assert.Contains("Abbigliamento", text);
        Assert.Contains("valorizzazione", text);
    }
}
