using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;

namespace Mauw.Erp.Infrastructure.Reporting.MigraDoc;

/// <summary>
/// MigraDoc/PdfSharp implementation of <see cref="IInvoicePdfGenerator"/>.
/// All MigraDoc types are strictly internal to this class; no MigraDoc type
/// ever surfaces in the Application or Domain layers.
/// </summary>
public sealed class MigraDocInvoicePdfGenerator : IInvoicePdfGenerator
{
    // ─── Palette ─────────────────────────────────────────────────────────────
    private static readonly Color ClrDark      = new(0xFF2D2D2D);
    private static readonly Color ClrBorder    = new(0xFF4A4A4A);
    private static readonly Color ClrBorderMid = new(0xFF888888);
    private static readonly Color ClrBorderFaint = new(0xFFCCCCCC);
    private static readonly Color ClrHeaderBg  = new(0xFF2D2D2D);
    private static readonly Color ClrBandBg    = new(0xFFEBEBEB);
    private static readonly Color ClrAltRow    = new(0xFFF6F6F6);

    // ─── Column widths for lines table (18.6 cm usable on A4 with 1.2 cm margins)
    // #(0.55) + SKU(2.30) + Desc(7.55) + Pz(0.90) + Price(2.00) + Disc(1.40) + VAT(1.15) + Tot(2.65) = 18.50
    private static readonly Unit[] LinesColWidths =
    [
        Unit.FromCentimeter(0.55),
        Unit.FromCentimeter(2.30),
        Unit.FromCentimeter(7.55),
        Unit.FromCentimeter(0.90),
        Unit.FromCentimeter(2.00),
        Unit.FromCentimeter(1.40),
        Unit.FromCentimeter(1.15),
        Unit.FromCentimeter(2.65),
    ];

    // ─── Public entry point ───────────────────────────────────────────────────

    public Task<byte[]> GenerateInvoicePdfAsync(InvoicePdfModel model, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(RenderPdf(model));
    }

    // ─── Document assembly ───────────────────────────────────────────────────

    private static byte[] RenderPdf(InvoicePdfModel model)
    {
        var doc      = BuildDocument(model);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms);
        return ms.ToArray();
    }

    private static Document BuildDocument(InvoicePdfModel model)
    {
        var doc = new Document();
        doc.Info.Title  = $"Fattura {model.DocumentNumber}";
        doc.Info.Author = model.Seller.BusinessName;

        SetDefaultStyle(doc);

        var section = doc.AddSection();
        ConfigurePage(section);

        // Native MigraDoc header/footer for multi-page support
        BuildFirstPageHeader(section.Headers.FirstPage, model);
        BuildContinuationHeader(section.Headers.Primary, model);
        BuildFooter(section.Footers.Primary, model);
        BuildFooter(section.Footers.FirstPage, model);

        // Content: lines table (with repeating header row) + summary
        BuildLinesTable(section, model);
        BuildSummarySection(section, model);

        return doc;
    }

    private static void SetDefaultStyle(Document doc)
    {
        var style = doc.Styles["Normal"]!;
        style.Font.Name  = "Arial";
        style.Font.Size  = Unit.FromPoint(9);
        style.Font.Color = ClrDark;
        style.ParagraphFormat.SpaceBefore = Unit.FromPoint(0);
        style.ParagraphFormat.SpaceAfter  = Unit.FromPoint(0);
    }

    private static void ConfigurePage(Section section)
    {
        var ps = section.PageSetup;
        ps.PageFormat   = PageFormat.A4;
        ps.Orientation  = Orientation.Portrait;
        ps.TopMargin    = Unit.FromCentimeter(1.2);
        ps.BottomMargin = Unit.FromCentimeter(1.5);
        ps.LeftMargin   = Unit.FromCentimeter(1.2);
        ps.RightMargin  = Unit.FromCentimeter(1.2);
        ps.HeaderDistance = Unit.FromCentimeter(0.5);
        ps.FooterDistance = Unit.FromCentimeter(0.5);
        ps.DifferentFirstPageHeaderFooter = true;
    }

    // ─── FIRST PAGE HEADER ───────────────────────────────────────────────────
    // Contains: Seller info + Doc-info box (top), Customer + Commercial info (bottom).

    private static void BuildFirstPageHeader(HeaderFooter header, InvoicePdfModel model)
    {
        BuildSellerDocInfoBlock(header, model);
        AddVerticalSpacer(header, 5);
        BuildCustomerCommercialBlock(header, model);
    }

    private static void BuildSellerDocInfoBlock(HeaderFooter header, InvoicePdfModel model)
    {
        // 2-column table: seller (left) | document info box (right)
        var table = header.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(11.5));
        table.AddColumn(Unit.FromCentimeter(7.1));

        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Top;

        // ── LEFT: seller identity ──
        var sc = row.Cells[0];

        var nameP = sc.AddParagraph(model.Seller.BusinessName);
        nameP.Format.Font.Size  = Unit.FromPoint(18);
        nameP.Format.Font.Bold  = true;
        nameP.Format.SpaceAfter = Unit.FromPoint(3);

        sc.AddParagraph(
            $"{model.Seller.Address}, {model.Seller.PostalCode} {model.Seller.City} ({model.Seller.Province}) - Italia"
        ).Format.Font.Size = Unit.FromPoint(8.5);

        var vatLine = $"C.F. / P.IVA: {model.Seller.VatNumber}";
        if (!string.IsNullOrWhiteSpace(model.Seller.Rea))         vatLine += $" | REA: {model.Seller.Rea}";
        if (!string.IsNullOrWhiteSpace(model.Seller.ShareCapital)) vatLine += $" | Cap. Soc. {model.Seller.ShareCapital}";
        sc.AddParagraph(vatLine).Format.Font.Size = Unit.FromPoint(8.5);

        var regimeLine = $"Regime Fiscale: {model.Seller.TaxRegime}";
        if (!string.IsNullOrWhiteSpace(model.Seller.Pec)) regimeLine += $" | PEC: {model.Seller.Pec}";
        sc.AddParagraph(regimeLine).Format.Font.Size = Unit.FromPoint(8.5);

        // ── RIGHT: document info box ──
        var dc = row.Cells[1];
        dc.Borders.Width = Unit.FromPoint(0.75);
        dc.Borders.Color = ClrBorder;

        AddDocInfoTitle(dc, $"FATTURA IMMEDIATA ({model.DocumentType})");
        AddDocInfoRow(dc, "Num. Fattura / Anno",              model.DocumentNumber,                   valueBold: true);
        AddDocInfoRow(dc, "Data Emissione",                   model.IssueDate.ToString("dd/MM/yyyy"));
        AddDocInfoRow(dc, "Divisa / Valuta",                  $"[{model.Currency}]",                  valueBold: true);
        AddDocInfoRow(dc, $"Pagamento ({model.Payment.PaymentMethod})", model.Payment.PaymentTerms);
        if (model.Payment.DueDate.HasValue)
            AddDocInfoRow(dc, "Scadenza 1ª Rata", model.Payment.DueDate.Value.ToString("dd/MM/yyyy"));
    }

    private static void AddDocInfoTitle(Cell cell, string title)
    {
        var p = cell.AddParagraph(title);
        p.Format.Font.Bold     = true;
        p.Format.Font.Size     = Unit.FromPoint(10);
        p.Format.Font.Color    = Colors.White;
        p.Format.Alignment     = ParagraphAlignment.Center;
        p.Format.SpaceBefore   = Unit.FromPoint(4);
        p.Format.SpaceAfter    = Unit.FromPoint(4);
        p.Format.Shading.Color = ClrHeaderBg;
        p.Format.Borders.Bottom.Width = Unit.FromPoint(0.5);
        p.Format.Borders.Bottom.Color = ClrBorder;
    }

    private static void AddDocInfoRow(Cell cell, string label, string value, bool valueBold = false)
    {
        var p = cell.AddParagraph();
        p.Format.Font.Size   = Unit.FromPoint(8.5);
        p.Format.SpaceBefore = Unit.FromPoint(2);
        p.Format.SpaceAfter  = Unit.FromPoint(2);
        p.Format.LeftIndent  = Unit.FromPoint(4);
        p.Format.TabStops.AddTabStop(Unit.FromCentimeter(3.5), TabAlignment.Left);
        p.Format.Borders.Bottom.Width = Unit.FromPoint(0.25);
        p.Format.Borders.Bottom.Color = ClrBorderFaint;

        var lbl = p.AddFormattedText(label);
        lbl.Bold      = true;
        lbl.Font.Size = Unit.FromPoint(8);
        p.AddTab();
        var val  = p.AddFormattedText(value);
        val.Bold = valueBold;
    }

    private static void BuildCustomerCommercialBlock(HeaderFooter header, InvoicePdfModel model)
    {
        // 2-column table: customer (left) | commercial / logistics (right)
        var table = header.AddTable();
        table.Borders.Width = Unit.FromPoint(0.5);
        table.Borders.Color = ClrBorder;
        table.AddColumn(Unit.FromCentimeter(9.2));
        table.AddColumn(Unit.FromCentimeter(9.4));

        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Top;
        row.Cells[0].Format.LeftIndent  = Unit.FromPoint(4);
        row.Cells[0].Format.RightIndent = Unit.FromPoint(4);
        row.Cells[1].Format.LeftIndent  = Unit.FromPoint(4);
        row.Cells[1].Format.RightIndent = Unit.FromPoint(4);

        // ── Customer cell ──
        var cc = row.Cells[0];
        AddCellSectionLabel(cc, "CESSIONARIO / COMMITTENTE (CLIENTE WHOLESALE)");

        var custName = cc.AddParagraph(model.Customer.BusinessName);
        custName.Format.Font.Bold = true;
        custName.Format.Font.Size = Unit.FromPoint(9);

        cc.AddParagraph(
            $"{model.Customer.Address}, {model.Customer.PostalCode} {model.Customer.City} ({model.Customer.Province}) - Italia"
        ).Format.Font.Size = Unit.FromPoint(8.5);

        var vatId = model.Customer.VatNumber ?? model.Customer.FiscalCode;
        if (!string.IsNullOrWhiteSpace(vatId))
        {
            var prefix = model.Customer.VatNumber is not null ? "P.IVA / C.F." : "C.F.";
            cc.AddParagraph($"{prefix}: {vatId}").Format.Font.Size = Unit.FromPoint(8.5);
        }

        if (!string.IsNullOrWhiteSpace(model.Customer.SdiCode) || !string.IsNullOrWhiteSpace(model.Customer.Pec))
        {
            var sdiP = cc.AddParagraph();
            sdiP.Format.Font.Size = Unit.FromPoint(8.5);
            if (!string.IsNullOrWhiteSpace(model.Customer.SdiCode))
            {
                sdiP.AddFormattedText("Cod. Destinatario SDI: ");
                sdiP.AddFormattedText(model.Customer.SdiCode!).Bold = true;
            }
            if (!string.IsNullOrWhiteSpace(model.Customer.Pec))
                sdiP.AddFormattedText($" | PEC: {model.Customer.Pec}");
        }

        // ── Commercial cell ──
        var comc = row.Cells[1];
        AddCellSectionLabel(comc, "DATI COMMERCIALI, LOGISTICA E AGENTE");

        var firstSeason = model.Lines
            .Select(l => l.SeasonCollection)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        if (!string.IsNullOrWhiteSpace(firstSeason))
            AddBoldLabelLine(comc, "Stagione / Collezione: ", firstSeason);

        if (!string.IsNullOrWhiteSpace(model.CustomerOrderRef))
        {
            var text = model.CustomerOrderDate.HasValue
                ? $"{model.CustomerOrderRef} del {model.CustomerOrderDate.Value:dd/MM/yyyy}"
                : model.CustomerOrderRef;
            AddBoldLabelLine(comc, "Rif. Ordine Cliente: ", text);
        }

        if (!string.IsNullOrWhiteSpace(model.DdtRef))
        {
            var text = !string.IsNullOrWhiteSpace(model.DeliveryTerms)
                ? $"{model.DdtRef} | Resa: {model.DeliveryTerms}"
                : model.DdtRef;
            AddBoldLabelLine(comc, "DDT / Consegna: ", text);
        }

        if (model.Agent is not null)
        {
            var agentLabel = model.Agent.Code is not null
                ? $"{model.Agent.Name} - Cod. {model.Agent.Code}"
                : model.Agent.Name;
            var ap = comc.AddParagraph();
            ap.Format.Font.Size = Unit.FromPoint(8.5);
            ap.AddFormattedText("Agente di Zona: ");
            ap.AddFormattedText(agentLabel).Bold = true;
            ap.AddFormattedText(" (se presente)").Italic = true;
        }
    }

    // ─── CONTINUATION HEADER (pages 2+) ──────────────────────────────────────

    private static void BuildContinuationHeader(HeaderFooter header, InvoicePdfModel model)
    {
        var table = header.AddTable();
        table.AddColumn(Unit.FromCentimeter(12.4));
        table.AddColumn(Unit.FromCentimeter(6.2));
        table.Borders.Top.Width    = Unit.FromPoint(0);
        table.Borders.Left.Width   = Unit.FromPoint(0);
        table.Borders.Right.Width  = Unit.FromPoint(0);
        table.Borders.Bottom.Width = Unit.FromPoint(0.75);
        table.Borders.Bottom.Color = ClrBorder;

        var row = table.AddRow();
        row.Shading.Color     = ClrBandBg;
        row.Height            = Unit.FromCentimeter(0.65);
        row.VerticalAlignment = VerticalAlignment.Center;

        var lc = row.Cells[0];
        lc.Borders.Visible  = false;
        lc.Format.LeftIndent = Unit.FromPoint(5);
        var lp = lc.AddParagraph($"DOC: {model.DocumentNumber}  —  CLIENTE: {model.Customer.BusinessName}");
        lp.Format.Font.Bold = true;
        lp.Format.Font.Size = Unit.FromPoint(8);

        var rc = row.Cells[1];
        rc.Borders.Visible     = false;
        rc.Format.Alignment    = ParagraphAlignment.Right;
        rc.Format.RightIndent  = Unit.FromPoint(5);
        rc.AddParagraph(model.Seller.BusinessName).Format.Font.Size = Unit.FromPoint(8);
    }

    // ─── FOOTER (all pages, via Primary + FirstPage) ─────────────────────────

    private static void BuildFooter(HeaderFooter footer, InvoicePdfModel model)
    {
        footer.Format.Borders.Top.Width = Unit.FromPoint(0.5);
        footer.Format.Borders.Top.Color = ClrBorder;

        var p = footer.AddParagraph();
        p.Format.Font.Size   = Unit.FromPoint(8);
        p.Format.Alignment   = ParagraphAlignment.Center;
        p.Format.SpaceBefore = Unit.FromPoint(4);

        p.AddFormattedText(
            $"{model.Seller.BusinessName}  |  Fattura N. {model.DocumentNumber} del {model.IssueDate:dd/MM/yyyy}  |  Pagina ");
        p.AddPageField();
        p.AddFormattedText(" di ");
        p.AddNumPagesField();
    }

    // ─── LINES TABLE ─────────────────────────────────────────────────────────

    private static void BuildLinesTable(Section section, InvoicePdfModel model)
    {
        AddVerticalSpacer(section, 6);

        var table = section.AddTable();
        table.Format.Font.Name = "Arial";
        table.Format.Font.Size = Unit.FromPoint(7.5);
        table.Borders.Width    = Unit.FromPoint(0.5);
        table.Borders.Color    = ClrBorder;

        foreach (var w in LinesColWidths)
            table.AddColumn(w);

        AddLinesHeaderRow(table);

        for (var i = 0; i < model.Lines.Count; i++)
            AddLineDataRow(table, model.Lines[i], i);
    }

    private static void AddLinesHeaderRow(Table table)
    {
        var hr = table.AddRow();
        // HeadingFormat = true → this row repeats as table heading on every page
        hr.HeadingFormat     = true;
        hr.Shading.Color     = ClrHeaderBg;
        hr.Height            = Unit.FromCentimeter(0.6);
        hr.VerticalAlignment = VerticalAlignment.Center;

        string[] labels =
        [
            "#",
            "SKU / Modello",
            "Descrizione Articolo (Modello / Colore / Taglia)",
            "Pz",
            "Prezzo Unit.",
            "Sconto",
            "IVA",
            "Importo Tot.",
        ];
        ParagraphAlignment[] aligns =
        [
            ParagraphAlignment.Center,
            ParagraphAlignment.Left,
            ParagraphAlignment.Left,
            ParagraphAlignment.Center,
            ParagraphAlignment.Right,
            ParagraphAlignment.Center,
            ParagraphAlignment.Center,
            ParagraphAlignment.Right,
        ];

        for (var i = 0; i < labels.Length; i++)
        {
            var cell = hr.Cells[i];
            cell.Format.Alignment   = aligns[i];
            cell.Format.LeftIndent  = Unit.FromPoint(2);
            cell.Format.RightIndent = Unit.FromPoint(2);
            var p = cell.AddParagraph(labels[i]);
            p.Format.Font.Bold  = true;
            p.Format.Font.Color = Colors.White;
            p.Format.Font.Size  = Unit.FromPoint(7.5);
        }
    }

    private static void AddLineDataRow(Table table, InvoicePdfLineModel line, int index)
    {
        var row = table.AddRow();
        row.Height            = Unit.FromCentimeter(0.52);
        row.VerticalAlignment = VerticalAlignment.Center;
        if (index % 2 == 1) row.Shading.Color = ClrAltRow;

        var discText = line.DiscountPercent.HasValue
            ? $"{line.DiscountPercent.Value:0.##}%"
            : "-";

        SetLineCell(row, 0, (index + 1).ToString("D2"),          ParagraphAlignment.Center);
        SetLineCell(row, 1, line.Sku,                             ParagraphAlignment.Left);
        SetLineCell(row, 2, line.Description,                     ParagraphAlignment.Left);
        SetLineCell(row, 3, line.Pieces.ToString(),               ParagraphAlignment.Center);
        SetLineCell(row, 4, line.UnitPrice.ToString("N2"),        ParagraphAlignment.Right);
        SetLineCell(row, 5, discText,                             ParagraphAlignment.Center);
        SetLineCell(row, 6, $"{line.VatRate:0.##}%",             ParagraphAlignment.Center);
        SetLineCell(row, 7, line.LineTotal.ToString("N2"),        ParagraphAlignment.Right);
    }

    private static void SetLineCell(Row row, int col, string text, ParagraphAlignment align)
    {
        var cell = row.Cells[col];
        cell.Format.Alignment   = align;
        cell.Format.LeftIndent  = Unit.FromPoint(2);
        cell.Format.RightIndent = Unit.FromPoint(2);
        cell.AddParagraph(text);
    }

    // ─── SUMMARY SECTION ─────────────────────────────────────────────────────

    private static void BuildSummarySection(Section section, InvoicePdfModel model)
    {
        AddVerticalSpacer(section, 8);

        // Row 1: bank/notes (left) | totals box (right)
        var outer = section.AddTable();
        outer.Borders.Visible = false;
        outer.AddColumn(Unit.FromCentimeter(9.7));
        outer.AddColumn(Unit.FromCentimeter(8.9));

        var r1 = outer.AddRow();
        r1.VerticalAlignment = VerticalAlignment.Top;
        BuildBankCell(r1.Cells[0], model);
        BuildTotalsCell(r1.Cells[1], model);

        // Row 2: empty left | VAT castelletto right
        var r2 = outer.AddRow();
        r2.VerticalAlignment = VerticalAlignment.Top;
        r2.Cells[0].Borders.Visible = false;
        BuildVatCastellettoCell(r2.Cells[1], model);
    }

    private static void BuildBankCell(Cell cell, InvoicePdfModel model)
    {
        cell.Borders.Width = Unit.FromPoint(0.5);
        cell.Borders.Color = ClrBorder;
        cell.Format.LeftIndent  = Unit.FromPoint(5);
        cell.Format.RightIndent = Unit.FromPoint(4);

        var title = cell.AddParagraph("COORDINATE BANCARIE E CONDIZIONI DI FORNITURA WHOLESALE");
        title.Format.Font.Bold  = true;
        title.Format.Font.Size  = Unit.FromPoint(7.5);
        title.Format.SpaceAfter = Unit.FromPoint(4);
        title.Format.Borders.Bottom.Width = Unit.FromPoint(0.5);
        title.Format.Borders.Bottom.Color = ClrBorder;

        if (!string.IsNullOrWhiteSpace(model.Payment.BankName))
            AddBoldLabelLine(cell, "Banca di Appoggio: ", model.Payment.BankName!);

        if (!string.IsNullOrWhiteSpace(model.Payment.Iban))
            AddBoldLabelLine(cell, "IBAN: ", model.Payment.Iban!);

        if (!string.IsNullOrWhiteSpace(model.Payment.Swift))
        {
            var p = cell.AddParagraph();
            p.Format.Font.Size = Unit.FromPoint(8.5);
            p.AddFormattedText("BIC / SWIFT: ");
            p.AddFormattedText(model.Payment.Swift!).Bold = true;
            p.AddFormattedText($" | Modalità FatturaPA: {model.Payment.PaymentMethod} - Bonifico");
        }

        if (model.Agent is not null)
        {
            var detail = model.Agent.Code is not null
                ? $"{model.Agent.Name} - Provvigione a ns. carico, nessun addebito al committente"
                : model.Agent.Name;
            AddBoldLabelLine(cell, "Dati Agente: ", detail);
        }

        if (!string.IsNullOrWhiteSpace(model.FiscalNotes))
        {
            var notesHdr = cell.AddParagraph("NOTE FISCALI / SDI E TRASPORTO");
            notesHdr.Format.Font.Bold   = true;
            notesHdr.Format.Font.Size   = Unit.FromPoint(7.5);
            notesHdr.Format.SpaceBefore = Unit.FromPoint(6);
            notesHdr.Format.SpaceAfter  = Unit.FromPoint(2);
            notesHdr.Format.Borders.Top.Width = Unit.FromPoint(0.5);
            notesHdr.Format.Borders.Top.Color = ClrBorder;

            cell.AddParagraph(model.FiscalNotes).Format.Font.Size = Unit.FromPoint(8);
        }
    }

    private static void BuildTotalsCell(Cell cell, InvoicePdfModel model)
    {
        // Outer border — bottom suppressed so VAT row reads as continuation
        cell.Borders.Width = Unit.FromPoint(0.5);
        cell.Borders.Color = ClrBorder;
        cell.Borders.Bottom.Width = Unit.FromPoint(0);

        const double tab = 5.9; // tab-stop (cm) inside 8.9 cm wide cell

        AddTotalRow(cell, "Totale Capi / Pezzi:",     $"{model.Totals.TotalPieces} Pz",         tab);
        AddTotalRow(cell, "Totale Merci Netto:",       Euro(model.Totals.TotalNet),               tab);
        AddTotalRow(cell, "Spese Spedizione / Assic.:", Euro(model.Totals.ShippingCost),          tab);
        AddTotalRow(cell, "Totale Imponibile:",        Euro(model.Totals.TotalNet + model.Totals.ShippingCost), tab);

        var vatRateLabel = model.VatSummary.Count == 1
            ? $"{model.VatSummary[0].VatRate:0.##}%"
            : "complessiva";
        AddTotalRow(cell, $"Totale IVA ({vatRateLabel}):", Euro(model.Totals.TotalVat), tab);

        // Grand total — highlighted row
        var gp = cell.AddParagraph();
        gp.Format.Font.Size    = Unit.FromPoint(10.5);
        gp.Format.Font.Bold    = true;
        gp.Format.Font.Color   = Colors.White;
        gp.Format.Shading.Color = ClrHeaderBg;
        gp.Format.SpaceBefore  = Unit.FromPoint(2);
        gp.Format.SpaceAfter   = Unit.FromPoint(5);
        gp.Format.LeftIndent   = Unit.FromPoint(5);
        gp.Format.RightIndent  = Unit.FromPoint(5);
        gp.Format.TabStops.AddTabStop(Unit.FromCentimeter(tab), TabAlignment.Right);
        gp.AddFormattedText("TOTALE FATTURA:");
        gp.AddTab();
        gp.AddFormattedText($"€ {model.Totals.TotalGross:N2}");
    }

    private static void AddTotalRow(Cell cell, string label, string value, double tabCm)
    {
        var p = cell.AddParagraph();
        p.Format.Font.Size   = Unit.FromPoint(8.5);
        p.Format.SpaceBefore = Unit.FromPoint(2);
        p.Format.SpaceAfter  = Unit.FromPoint(2);
        p.Format.LeftIndent  = Unit.FromPoint(5);
        p.Format.RightIndent = Unit.FromPoint(5);
        p.Format.TabStops.AddTabStop(Unit.FromCentimeter(tabCm), TabAlignment.Right);
        p.Format.Borders.Bottom.Width = Unit.FromPoint(0.25);
        p.Format.Borders.Bottom.Color = ClrBorderFaint;

        p.AddFormattedText(label).Bold = true;
        p.AddTab();
        p.AddFormattedText(value);
    }

    private static void BuildVatCastellettoCell(Cell cell, InvoicePdfModel model)
    {
        // Seamless continuation of the totals cell — only top border suppressed
        cell.Borders.Width = Unit.FromPoint(0.5);
        cell.Borders.Color = ClrBorder;
        cell.Borders.Top.Width = Unit.FromPoint(0);
        cell.Format.LeftIndent  = Unit.FromPoint(5);
        cell.Format.RightIndent = Unit.FromPoint(5);

        // VAT header row (using tab stops to simulate columns)
        // Cols: Aliquota/Natura | Imponibile | Imposta | Esigibilità
        const double t1 = 2.4;
        const double t2 = 4.4;
        const double t3 = 6.2;

        var hdr = cell.AddParagraph();
        hdr.Format.Font.Size    = Unit.FromPoint(7.5);
        hdr.Format.Font.Bold    = true;
        hdr.Format.SpaceBefore  = Unit.FromPoint(2);
        hdr.Format.SpaceAfter   = Unit.FromPoint(2);
        hdr.Format.Shading.Color = ClrBandBg;
        SetVatTabStops(hdr, t1, t2, t3);
        hdr.AddFormattedText("Aliquota / Natura");
        hdr.AddTab(); hdr.AddFormattedText("Imponibile");
        hdr.AddTab(); hdr.AddFormattedText("Imposta");
        hdr.AddTab(); hdr.AddFormattedText("Esigibilità");

        foreach (var vat in model.VatSummary)
        {
            var nature = string.IsNullOrWhiteSpace(vat.ExemptionNature) ? "I (Immediata)" : vat.ExemptionNature;
            var rateLabel = string.IsNullOrWhiteSpace(vat.ExemptionNature)
                ? $"{vat.VatRate:0.##}% (Ordinaria)"
                : $"{vat.VatRate:0.##}% — {vat.ExemptionNature}";

            var vp = cell.AddParagraph();
            vp.Format.Font.Size   = Unit.FromPoint(8.5);
            vp.Format.SpaceBefore = Unit.FromPoint(2);
            vp.Format.SpaceAfter  = Unit.FromPoint(2);
            vp.Format.Borders.Bottom.Width = Unit.FromPoint(0.25);
            vp.Format.Borders.Bottom.Color = ClrBorderFaint;
            SetVatTabStops(vp, t1, t2, t3);

            vp.AddFormattedText(rateLabel);
            vp.AddTab(); vp.AddFormattedText(Euro(vat.TaxableAmount));
            vp.AddTab(); vp.AddFormattedText(Euro(vat.VatAmount));
            vp.AddTab(); vp.AddFormattedText(nature);
        }
    }

    private static void SetVatTabStops(Paragraph p, double t1, double t2, double t3)
    {
        p.Format.TabStops.AddTabStop(Unit.FromCentimeter(t1), TabAlignment.Right);
        p.Format.TabStops.AddTabStop(Unit.FromCentimeter(t2), TabAlignment.Right);
        p.Format.TabStops.AddTabStop(Unit.FromCentimeter(t3), TabAlignment.Right);
    }

    // ─── Small helpers ────────────────────────────────────────────────────────

    private static void AddCellSectionLabel(Cell cell, string text)
    {
        var p = cell.AddParagraph(text);
        p.Format.Font.Bold    = true;
        p.Format.Font.Size    = Unit.FromPoint(7);
        p.Format.SpaceAfter   = Unit.FromPoint(3);
        p.Format.Borders.Bottom.Width = Unit.FromPoint(0.25);
        p.Format.Borders.Bottom.Color = ClrBorderMid;
    }

    private static void AddBoldLabelLine(Cell cell, string label, string value)
    {
        var p = cell.AddParagraph();
        p.Format.Font.Size = Unit.FromPoint(8.5);
        p.AddFormattedText(label);
        p.AddFormattedText(value).Bold = true;
    }

    private static void AddBoldLabelLine(HeaderFooter _hf, string label, string value)
    {
        // Unused overload kept for symmetry; BuildCustomerCommercialBlock uses Cell version.
        throw new NotSupportedException();
    }

    private static void AddVerticalSpacer(HeaderFooter header, double points)
    {
        var p = header.AddParagraph();
        p.Format.SpaceBefore = Unit.FromPoint(points);
        p.Format.SpaceAfter  = Unit.FromPoint(0);
    }

    private static void AddVerticalSpacer(Section section, double points)
    {
        var p = section.AddParagraph();
        p.Format.SpaceBefore = Unit.FromPoint(points);
        p.Format.SpaceAfter  = Unit.FromPoint(0);
    }

    private static string Euro(decimal amount) => $"€ {amount:N2}";
}
