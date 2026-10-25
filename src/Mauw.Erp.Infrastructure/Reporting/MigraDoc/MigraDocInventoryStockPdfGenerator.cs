using System.Globalization;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Enums;
using Mauw.Erp.Contracts.Interfaces;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Mauw.Erp.Infrastructure.Reporting.MigraDoc;

public sealed class MigraDocInventoryStockPdfGenerator : IInventoryStockPdfGenerator
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");
    private static readonly Color HeaderColor = new(0xFF1F2937);
    private static readonly Color BorderColor = new(0xFFCBD5E1);

    public byte[] Generate(InventoryStockExportDto report, CompanySettingsDto company)
    {
        var document = new Document();
        document.Info.Title = "Inventario di magazzino";
        document.Info.Author = company.BusinessName;

        var normal = document.Styles["Normal"]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = Unit.FromPoint(8);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.Format.Font.Size = Unit.FromPoint(7);
        footer.AddText("Pag. ");
        footer.AddPageField();
        footer.AddText(" di ");
        footer.AddNumPagesField();

        var title = section.AddParagraph(company.BusinessName);
        title.Format.Font.Size = Unit.FromPoint(15);
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(3);
        section.AddParagraph($"Partita IVA: {company.VatNumber ?? "—"}");
        if (company.LegalHeadquarters is { } legalAddress)
        {
            var address = string.Join(", ", new[]
            {
                legalAddress.Street,
                $"{legalAddress.ZipCode} {legalAddress.City}".Trim(),
                legalAddress.Province,
                legalAddress.CountryCode
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            section.AddParagraph($"Sede legale: {address}");
        }

        var subtitle = section.AddParagraph("Inventario / Giacenze di magazzino");
        subtitle.Format.Font.Size = Unit.FromPoint(12);
        subtitle.Format.Font.Bold = true;
        subtitle.Format.SpaceBefore = Unit.FromPoint(8);
        subtitle.Format.SpaceAfter = Unit.FromPoint(2);
        section.AddParagraph($"Generato il {report.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm:ss", ItalianCulture)} UTC");
        if (report.AtDate.HasValue)
            section.AddParagraph($"Giacenza fisica al {report.AtDate.Value.ToString("dd/MM/yyyy HH:mm:ss", ItalianCulture)} UTC");
        var includeCosts = report.IncludesCosts;
        if (includeCosts)
            section.AddParagraph($"Criterio di valorizzazione: {InventoryStockValuationLabels.GetLabel(report.ValuationMethod)}");
        section.AddParagraph(FormatFilters(report.Filters));
        if (!string.IsNullOrWhiteSpace(report.AllocationNote))
            section.AddParagraph(report.AllocationNote);

        var table = section.AddTable();
        table.Borders.Color = BorderColor;
        table.Borders.Width = Unit.FromPoint(0.5);
        table.LeftPadding = Unit.FromPoint(3);
        table.RightPadding = Unit.FromPoint(3);
        table.TopPadding = Unit.FromPoint(1.5);
        table.BottomPadding = Unit.FromPoint(1.5);

        var widths = includeCosts
            ? new[] { 2.1, 10.0, 2.6, 2.0, 2.1, 1.1, 2.2, 2.3 }
            : new[] { 2.1, 15.0, 3.6, 2.6, 2.6, 1.4 };
        foreach (var width in widths)
            table.AddColumn(Unit.FromCentimeter(width));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = HeaderColor;
        header.Format.Font.Color = Colors.White;
        header.Format.Font.Bold = true;
        header.Format.Font.Size = Unit.FromPoint(7);
        var headings = new List<string>
        {
            "SKU",
            "Descrizione",
            "Taglia / Colore",
            "Deposito",
            "Giacenza fisica",
            "UM"
        };
        if (includeCosts)
        {
            headings.Add(InventoryStockValuationLabels.GetLabel(report.ValuationMethod));
            headings.Add("Valore totale");
        }
        for (var i = 0; i < headings.Count; i++)
            header.Cells[i].AddParagraph(headings[i]);

        foreach (var item in report.Items)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(item.Sku);
            row.Cells[1].AddParagraph(item.Description);
            row.Cells[2].AddParagraph(string.Join(" / ", new[] { item.Size, item.Color }.Where(x => !string.IsNullOrWhiteSpace(x))));
            row.Cells[3].AddParagraph(item.WarehouseCode);
            row.Cells[4].AddParagraph(FormatQuantity(item.QuantityOnHand));
            row.Cells[5].AddParagraph(item.UnitOfMeasure);
            row.Cells[4].Format.Alignment = ParagraphAlignment.Right;
            if (!includeCosts)
                continue;
            row.Cells[6].AddParagraph(FormatUnitCost(item.UnitCost));
            row.Cells[7].AddParagraph(FormatMoney(item.TotalValue));
            row.Cells[6].Format.Alignment = ParagraphAlignment.Right;
            row.Cells[7].Format.Alignment = ParagraphAlignment.Right;
        }

        var total = table.AddRow();
        total.Shading.Color = new Color(0xFFF1F5F9);
        total.Format.Font.Bold = true;
        total.Cells[0].AddParagraph("TOTALI");
        total.Cells[4].AddParagraph(FormatQuantity(report.Totals.TotalPhysicalQuantity));
        total.Cells[4].Format.Alignment = ParagraphAlignment.Right;
        if (includeCosts)
        {
            total.Cells[7].AddParagraph(FormatMoney(report.Totals.TotalValue));
            total.Cells[7].Format.Alignment = ParagraphAlignment.Right;
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output);
        return output.ToArray();
    }

    private static string FormatQuantity(decimal quantity) =>
        quantity.ToString("0.###", ItalianCulture);

    private static string FormatMoney(decimal? amount) =>
        amount.HasValue ? amount.Value.ToString("N2", ItalianCulture) + " €" : "—";

    private static string FormatUnitCost(decimal? amount) =>
        amount.HasValue ? amount.Value.ToString("0.00######", ItalianCulture) + " €" : "—";

    private static string FormatFilters(InventoryStockFilterSummaryDto? filters)
    {
        if (filters is null)
            return "Filtri applicati: nessuno";

        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(filters.Search))
            values.Add($"Ricerca: {filters.Search}");
        if (!string.IsNullOrWhiteSpace(filters.Warehouse))
            values.Add($"Deposito: {filters.Warehouse}");
        if (!string.IsNullOrWhiteSpace(filters.Category))
            values.Add($"Categoria: {filters.Category}");
        if (filters.OnlyInStock)
            values.Add("Solo articoli con giacenza");
        return values.Count == 0
            ? "Filtri applicati: nessuno"
            : $"Filtri applicati: {string.Join("; ", values)}";
    }
}
