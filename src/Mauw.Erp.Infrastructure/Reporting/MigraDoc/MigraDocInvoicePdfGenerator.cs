using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Application.Services;

namespace Mauw.Erp.Infrastructure.Reporting.MigraDoc;

/// <summary>
/// MigraDoc/PdfSharp implementation of <see cref="IInvoicePdfGenerator"/>.
/// All MigraDoc types are strictly internal to this class; no MigraDoc type
/// ever surfaces in the Application or Domain layers.
/// </summary>
public sealed class MigraDocInvoicePdfGenerator(ICompanyLogoService companyLogos) : IInvoicePdfGenerator
{
    // ─── Palette ─────────────────────────────────────────────────────────────
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");
    private const string CourtesyCopyNotice =
        "Copia di cortesia priva di valenza fiscale ai sensi dell'art. 21 D.P.R. 633/72. " +
        "L'originale della fattura elettronica è trasmesso tramite SDI ed è disponibile " +
        "nell'area riservata dell'Agenzia delle Entrate.";
    private static readonly Color ClrDark = new(0xFF1F2937);
    private static readonly Color ClrBorder = new(0xFFCBD5E1);
    private static readonly Color ClrBorderMid = new(0xFF888888);
    private static readonly Color ClrHeaderBg = new(0xFF1F2937);
    private static readonly Color ClrBandBg = new(0xFFF1F5F9);
    private static readonly Color ClrAltRow = new(0xFFF8FAFC);

    // All blocks use the same 18 cm printable width.
    private static readonly Unit[] LinesColWidths =
    [
        Unit.FromCentimeter(0.60),
        Unit.FromCentimeter(2.30),
        Unit.FromCentimeter(7.25),
        Unit.FromCentimeter(0.90),
        Unit.FromCentimeter(2.00),
        Unit.FromCentimeter(1.30),
        Unit.FromCentimeter(1.15),
        Unit.FromCentimeter(2.50),
    ];

    // ─── Public entry point ───────────────────────────────────────────────────

    public async Task<byte[]> GenerateInvoicePdfAsync(InvoicePdfModel model, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var logo = await companyLogos.GetAsync("compact", ct);
        if (!logo.Success)
            throw new InvalidOperationException(logo.Error);
        return RenderPdf(model, logo.Data!.PngData);
    }

    // ─── Document assembly ───────────────────────────────────────────────────

    private static byte[] RenderPdf(InvoicePdfModel model, byte[] logoPng)
    {
        var doc = BuildDocument(model, logoPng);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms);
        return ms.ToArray();
    }

    private static Document BuildDocument(InvoicePdfModel model, byte[] logoPng)
    {
        var doc = new Document();
        doc.Info.Title = $"{GetDocumentTitle(model)} {model.DocumentNumber}";
        doc.Info.Author = model.Seller.BusinessName;

        SetDefaultStyle(doc);

        var section = doc.AddSection();
        ConfigurePage(section);

        // The full heading must flow with the body, not overlap it as a fixed header.
        BuildContinuationHeader(section.Headers.Primary, model);
        BuildFooter(section.Footers.Primary, model);
        BuildFooter(section.Footers.FirstPage, model);

        BuildFirstPageHeader(section, model, logoPng);
        BuildLinesTable(section, model);
        BuildSummarySection(section, model);

        return doc;
    }

    private static void SetDefaultStyle(Document doc)
    {
        var style = doc.Styles["Normal"]!;
        style.Font.Name = "Arial";
        style.Font.Size = Unit.FromPoint(9);
        style.Font.Color = ClrDark;
        style.ParagraphFormat.SpaceBefore = Unit.FromPoint(0);
        style.ParagraphFormat.SpaceAfter = Unit.FromPoint(0);
    }

    private static void ConfigurePage(Section section)
    {
        var ps = section.PageSetup;
        ps.PageFormat = PageFormat.A4;
        ps.Orientation = Orientation.Portrait;
        ps.TopMargin = Unit.FromCentimeter(1.8);
        ps.BottomMargin = Unit.FromCentimeter(2.3);
        ps.LeftMargin = Unit.FromCentimeter(1.5);
        ps.RightMargin = Unit.FromCentimeter(1.5);
        ps.HeaderDistance = Unit.FromCentimeter(0.6);
        ps.FooterDistance = Unit.FromCentimeter(0.6);
        ps.DifferentFirstPageHeaderFooter = true;
    }

    // ─── FIRST PAGE HEADER ───────────────────────────────────────────────────
    // Contains: Seller info + Doc-info box (top), Customer + Commercial info (bottom).

    private static void BuildFirstPageHeader(Section section, InvoicePdfModel model, byte[] logoPng)
    {
        BuildSellerDocInfoBlock(section, model, logoPng);
        AddVerticalSpacer(section, 5);
        BuildCustomerCommercialBlock(section, model);
    }

    private static void BuildSellerDocInfoBlock(Section section, InvoicePdfModel model, byte[] logoPng)
    {
        var details = new List<(string Label, string Value)>
        {
            ($"Num. {GetDocumentTitle(model)} / Anno", model.DocumentNumber),
            ("Data Emissione", model.IssueDate.ToString("dd/MM/yyyy", ItalianCulture)),
            ("Divisa / Valuta", model.Currency),
            ("Modalità pagamento", model.Payment.PaymentMethod)
        };
        if (model.Payment.DueDate is { } dueDate)
            details.Add(("Scadenza", dueDate.ToString("dd/MM/yyyy", ItalianCulture)));
        if (model.Payment.Amount is { } amount)
            details.Add(("Importo da pagare", Money(amount, model.Currency)));

        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(1.3));
        table.AddColumn(Unit.FromCentimeter(0.2));
        table.AddColumn(Unit.FromCentimeter(8.1));
        table.AddColumn(Unit.FromCentimeter(0.3));
        table.AddColumn(Unit.FromCentimeter(8.1));
        table.LeftPadding = Unit.FromPoint(0);
        table.RightPadding = Unit.FromPoint(0);

        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Top;
        using var stream = new MemoryStream(logoPng, writable: false);
        using var image = PdfSharp.Drawing.XImage.FromStream(stream);
        var logo = row.Cells[0].AddParagraph().AddImage($"base64:{Convert.ToBase64String(logoPng)}");
        logo.LockAspectRatio = true;
        logo.Width = Unit.FromCentimeter(Math.Min(1.2, 1.35 * image.PixelWidth / image.PixelHeight));
        var sc = row.Cells[2];
        var nameP = sc.AddParagraph(model.Seller.BusinessName);
        nameP.Format.Font.Size = Unit.FromPoint(12);
        nameP.Format.Font.Bold = true;
        nameP.Format.SpaceAfter = Unit.FromPoint(3);

        sc.AddParagraph(
            $"{model.Seller.Address}, {model.Seller.PostalCode} {model.Seller.City} ({model.Seller.Province}) - Italia"
        ).Format.Font.Size = Unit.FromPoint(8.5);

        sc.AddParagraph($"C.F. / P.IVA: {model.Seller.VatNumber}").Format.Font.Size = Unit.FromPoint(8.5);

        var registryLine = BuildRegistryLine(model.Seller);
        if (registryLine is not null)
            sc.AddParagraph(registryLine).Format.Font.Size = Unit.FromPoint(6.5);

        var capitalLine = BuildShareCapitalLine(model.Seller);
        if (capitalLine is not null)
        {
            var capitalParagraph = sc.AddParagraph(capitalLine);
            capitalParagraph.Format.Font.Size = Unit.FromPoint(6.5);
        }

        var regimeLine = $"Regime Fiscale: {model.Seller.TaxRegime}";
        if (!string.IsNullOrWhiteSpace(model.Seller.Pec)) regimeLine += $" | PEC: {model.Seller.Pec}";
        sc.AddParagraph(regimeLine).Format.Font.Size = Unit.FromPoint(6.5);

        var legalStatusLine = BuildLegalStatusLine(model.Seller);
        if (legalStatusLine is not null)
            sc.AddParagraph(legalStatusLine).Format.Font.Size = Unit.FromPoint(6.5);

        var dc = row.Cells[4];
        var title = IsInvoice(model)
            ? $"{GetDocumentHeader(model)} ({model.DocumentType})"
            : GetDocumentHeader(model);
        AddDocInfoTitle(dc, title);
        // Independent cells let either side grow without overflowing a vertically merged range.
        var detailsTable = new Table();
        detailsTable.AddColumn(Unit.FromCentimeter(4.0));
        detailsTable.AddColumn(Unit.FromCentimeter(4.1));
        detailsTable.LeftPadding = Unit.FromPoint(0);
        detailsTable.RightPadding = Unit.FromPoint(0);
        dc.Elements.Add(detailsTable);
        foreach (var (label, value) in details)
            AddDocInfoRow(detailsTable, label, value);
    }

    private static void AddDocInfoTitle(Cell cell, string title)
    {
        var p = cell.AddParagraph(title);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = Unit.FromPoint(10);
        p.Format.Font.Color = ClrDark;
        p.Format.Alignment = ParagraphAlignment.Right;
        p.Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static void AddDocInfoRow(Table table, string label, string value)
    {
        var row = table.AddRow();
        row.TopPadding = Unit.FromPoint(2);
        row.BottomPadding = Unit.FromPoint(2);
        for (var i = 0; i <= 1; i++)
        {
            row.Cells[i].Borders.Width = Unit.FromPoint(0.5);
            row.Cells[i].Borders.Color = ClrBorder;
            row.Cells[i].Borders.Visible = true;
            row.Cells[i].Format.LeftIndent = Unit.FromPoint(4);
            row.Cells[i].Format.RightIndent = Unit.FromPoint(4);
            row.Cells[i].Format.Font.Size = Unit.FromPoint(8);
        }

        row.Cells[0].Shading.Color = ClrBandBg;
        row.Cells[0].Format.Font.Bold = true;
        row.Cells[0].AddParagraph(label);
        row.Cells[1].Format.Alignment = ParagraphAlignment.Right;
        row.Cells[1].AddParagraph(value);
    }

    private static void BuildCustomerCommercialBlock(Section section, InvoicePdfModel model)
    {
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.5);
        table.Borders.Color = ClrBorder;
        table.AddColumn(Unit.FromCentimeter(8.85));
        table.AddColumn(Unit.FromCentimeter(0.3));
        table.AddColumn(Unit.FromCentimeter(8.85));

        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Top;
        row.TopPadding = Unit.FromPoint(5);
        row.BottomPadding = Unit.FromPoint(5);
        row.Cells[1].Borders.Visible = false;
        row.Cells[0].Format.LeftIndent = Unit.FromPoint(4);
        row.Cells[0].Format.RightIndent = Unit.FromPoint(4);
        row.Cells[2].Format.LeftIndent = Unit.FromPoint(4);
        row.Cells[2].Format.RightIndent = Unit.FromPoint(4);

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
        var comc = row.Cells[2];
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
        }
    }

    // ─── CONTINUATION HEADER (pages 2+) ──────────────────────────────────────

    private static void BuildContinuationHeader(HeaderFooter header, InvoicePdfModel model)
    {
        var table = header.AddTable();
        table.AddColumn(Unit.FromCentimeter(18));
        table.Borders.Width = Unit.FromPoint(0.5);
        table.Borders.Color = ClrBorder;

        var row = table.AddRow();
        row.Shading.Color = ClrBandBg;
        row.Height = Unit.FromCentimeter(0.65);
        row.VerticalAlignment = VerticalAlignment.Center;

        var lc = row.Cells[0];
        lc.Format.LeftIndent = Unit.FromPoint(5);
        lc.Format.RightIndent = Unit.FromPoint(5);
        var lp = lc.AddParagraph($"DOC: {model.DocumentNumber}  —  CLIENTE: {model.Customer.BusinessName}");
        lp.Format.Font.Bold = true;
        lp.Format.Font.Size = Unit.FromPoint(8);
    }

    // ─── FOOTER (all pages, via Primary + FirstPage) ─────────────────────────

    private static void BuildFooter(HeaderFooter footer, InvoicePdfModel model)
    {
        var table = footer.AddTable();
        table.AddColumn(Unit.FromCentimeter(15));
        table.AddColumn(Unit.FromCentimeter(3));
        table.Borders.Top.Width = Unit.FromPoint(0.5);
        table.Borders.Top.Color = ClrBorder;
        table.Format.Font.Size = Unit.FromPoint(6);
        var row = table.AddRow();
        row.TopPadding = Unit.FromPoint(2);
        var companyCell = row.Cells[0];
        var footerDetails = new[]
        {
            model.Seller.BusinessName,
            BuildFooterLegalStatus(model.Seller),
            BuildShareCapitalLine(model.Seller),
            BuildFooterRegistryLine(model.Seller)
        }.Where(value => !string.IsNullOrWhiteSpace(value));
        companyCell.AddParagraph(string.Join(" | ", footerDetails));
        if (IsFiscalDocument(model))
        {
            var notice = companyCell.AddParagraph(CourtesyCopyNotice);
            notice.Format.Font.Size = Unit.FromPoint(6);
        }

        var p = row.Cells[1].AddParagraph("Pagina ");
        p.Format.Alignment = ParagraphAlignment.Right;
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
        table.Format.Font.Size = Unit.FromPoint(8);
        table.Borders.Width = Unit.FromPoint(0.5);
        table.Borders.Color = ClrBorder;
        table.TopPadding = Unit.FromPoint(3);
        table.BottomPadding = Unit.FromPoint(3);

        foreach (var w in LinesColWidths)
            table.AddColumn(w);

        AddLinesHeaderRow(table);

        var orderedLines = model.Lines
            .OrderBy(line => line.ModelCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.Color ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => GetSizeSortOrder(line.Size))
            .ThenBy(line => line.Size ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var i = 0; i < orderedLines.Count; i++)
            AddLineDataRow(table, orderedLines[i], i);
    }

    private static void AddLinesHeaderRow(Table table)
    {
        var hr = table.AddRow();
        // HeadingFormat = true → this row repeats as table heading on every page
        hr.HeadingFormat = true;
        hr.Shading.Color = ClrHeaderBg;
        hr.Height = Unit.FromCentimeter(0.75);
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
            ParagraphAlignment.Right,
            ParagraphAlignment.Right,
            ParagraphAlignment.Center,
            ParagraphAlignment.Center,
            ParagraphAlignment.Right,
        ];

        for (var i = 0; i < labels.Length; i++)
        {
            var cell = hr.Cells[i];
            cell.Format.Alignment = aligns[i];
            cell.Format.LeftIndent = Unit.FromPoint(2);
            cell.Format.RightIndent = Unit.FromPoint(2);
            var p = cell.AddParagraph(labels[i]);
            p.Format.Font.Bold = true;
            p.Format.Font.Color = Colors.White;
            p.Format.Font.Size = Unit.FromPoint(7.5);
        }
    }

    private static void AddLineDataRow(Table table, InvoicePdfLineModel line, int index)
    {
        var row = table.AddRow();
        row.Height = Unit.FromCentimeter(0.8);
        row.VerticalAlignment = VerticalAlignment.Center;
        if (index % 2 == 1) row.Shading.Color = ClrAltRow;

        var discText = line.DiscountPercent.HasValue
            ? $"{line.DiscountPercent.Value.ToString("0.##", ItalianCulture)}%"
            : "-";

        SetLineCell(row, 0, (index + 1).ToString("D2"), ParagraphAlignment.Center);
        SetLineCell(row, 1, line.Sku, ParagraphAlignment.Left);
        SetLineCell(row, 2, line.Description, ParagraphAlignment.Left);
        SetLineCell(row, 3, line.Pieces.ToString("N0", ItalianCulture), ParagraphAlignment.Right);
        SetLineCell(row, 4,
            Math.Round(line.UnitPrice, 2, MidpointRounding.AwayFromZero).ToString("N2", ItalianCulture),
            ParagraphAlignment.Right);
        SetLineCell(row, 5, discText, ParagraphAlignment.Center);
        SetLineCell(row, 6, line.NatureCode is null
            ? $"{line.VatRate.ToString("0.##", ItalianCulture)}%"
            : $"0%\n{line.NatureCode}", ParagraphAlignment.Center);
        SetLineCell(row, 7, line.LineTotal.ToString("N2", ItalianCulture), ParagraphAlignment.Right);
    }

    private static void SetLineCell(Row row, int col, string text, ParagraphAlignment align)
    {
        var cell = row.Cells[col];
        cell.Format.Alignment = align;
        cell.Format.LeftIndent = Unit.FromPoint(2);
        cell.Format.RightIndent = Unit.FromPoint(2);
        cell.AddParagraph(text);
    }

    // ─── SUMMARY SECTION ─────────────────────────────────────────────────────

    private static void BuildSummarySection(Section section, InvoicePdfModel model)
    {
        AddVerticalSpacer(section, 8);

        var outer = section.AddTable();
        outer.Borders.Visible = false;
        outer.AddColumn(Unit.FromCentimeter(9.7));
        outer.AddColumn(Unit.FromCentimeter(0.3));
        outer.AddColumn(Unit.FromCentimeter(2.3));
        outer.AddColumn(Unit.FromCentimeter(2.3));
        outer.AddColumn(Unit.FromCentimeter(1.6));
        outer.AddColumn(Unit.FromCentimeter(1.8));
        outer.LeftPadding = Unit.FromPoint(0);
        outer.RightPadding = Unit.FromPoint(0);
        outer.TopPadding = Unit.FromPoint(3);
        outer.BottomPadding = Unit.FromPoint(3);

        BuildTotalsRows(outer, model);
        BuildVatCastellettoRows(outer, model);

        // One grid, rather than tab stops or nested tables, keeps all values in their columns.
        var first = outer.Rows[0];
        first.KeepWith = outer.Rows.Count - 1;
        first.Cells[0].MergeDown = outer.Rows.Count - 1;
        first.Cells[1].MergeDown = outer.Rows.Count - 1;
        BuildBankCell(first.Cells[0], model);
    }

    private static void BuildBankCell(Cell cell, InvoicePdfModel model)
    {
        cell.Borders.Width = Unit.FromPoint(0.5);
        cell.Borders.Color = ClrBorder;
        cell.Borders.Visible = true;
        cell.Shading.Color = ClrAltRow;
        cell.VerticalAlignment = VerticalAlignment.Top;
        cell.Format.LeftIndent = Unit.FromPoint(5);
        cell.Format.RightIndent = Unit.FromPoint(4);

        var title = cell.AddParagraph("COORDINATE BANCARIE E CONDIZIONI DI FORNITURA WHOLESALE");
        title.Format.Font.Bold = true;
        title.Format.Font.Size = Unit.FromPoint(7.5);
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
        }

        BuildPaymentSummary(cell, model);

        if (model.Agent is not null)
        {
            var detail = $"{model.Agent.Name} - Provvigione a ns. carico, nessun addebito al committente";
            AddBoldLabelLine(cell, "Dati Agente: ", detail);
        }

        if (!string.IsNullOrWhiteSpace(model.FiscalNotes))
        {
            var fiscalDocument = IsInvoice(model) ||
                                 model.DocumentType.Equals("NOTA CREDITO", StringComparison.OrdinalIgnoreCase);
            var notesHdr = cell.AddParagraph(fiscalDocument ? "NOTE FISCALI / SDI E TRASPORTO" : "NOTE E CONDIZIONI");
            notesHdr.Format.Font.Bold = true;
            notesHdr.Format.Font.Size = Unit.FromPoint(7.5);
            notesHdr.Format.SpaceBefore = Unit.FromPoint(6);
            notesHdr.Format.SpaceAfter = Unit.FromPoint(2);
            notesHdr.Format.Borders.Top.Width = Unit.FromPoint(0.5);
            notesHdr.Format.Borders.Top.Color = ClrBorder;

            cell.AddParagraph(model.FiscalNotes).Format.Font.Size = Unit.FromPoint(8);
        }
    }

    private static void BuildTotalsRows(Table table, InvoicePdfModel model)
    {
        AddTotalRow(table, "Totale Capi / Pezzi:", $"{model.Totals.TotalPieces} Pz");
        AddTotalRow(table, "Totale Merci Netto:", Money(model.Totals.TotalNet, model.Currency));
        AddTotalRow(table, "Spese Spedizione / Assic.:", Money(model.Totals.ShippingCost, model.Currency));
        AddTotalRow(table, "Totale Imponibile:",
            Money(model.Totals.TotalNet + model.Totals.ShippingCost, model.Currency));

        var vatRateLabel = model.VatSummary.Count == 1
            ? $"{model.VatSummary[0].VatRate.ToString("0.##", ItalianCulture)}%"
            : "complessiva";
        AddTotalRow(table, $"Totale IVA ({vatRateLabel}):", Money(model.Totals.TotalVat, model.Currency));
        AddTotalRow(table, GetGrandTotalLabel(model), Money(model.Totals.TotalGross, model.Currency), grandTotal: true);
    }

    private static bool IsInvoice(InvoicePdfModel model) =>
        model.DocumentType.StartsWith("TD", StringComparison.OrdinalIgnoreCase);

    private static string GetDocumentTitle(InvoicePdfModel model) =>
        model.DocumentType.ToUpperInvariant() switch
        {
            "ORDINE" => "Ordine",
            "DDT" => "DDT",
            "NOTA CREDITO" or "TD04" => "Nota credito",
            _ => "Fattura"
        };

    private static string GetDocumentHeader(InvoicePdfModel model) =>
        model.DocumentType.ToUpperInvariant() switch
        {
            "ORDINE" => "ORDINE",
            "DDT" => "DDT",
            "NOTA CREDITO" or "TD04" => "NOTA CREDITO",
            _ => "FATTURA IMMEDIATA"
        };

    private static string GetGrandTotalLabel(InvoicePdfModel model) =>
        model.DocumentType.ToUpperInvariant() switch
        {
            "ORDINE" => "TOTALE ORDINE:",
            "DDT" => "TOTALE DDT:",
            "NOTA CREDITO" or "TD04" => "TOTALE NOTA CREDITO:",
            _ => "TOTALE FATTURA:"
        };

    private static void AddTotalRow(Table table, string label, string value, bool grandTotal = false)
    {
        var row = table.AddRow();
        row.Cells[2].MergeRight = 1;
        row.Cells[4].MergeRight = 1;
        foreach (var index in new[] { 2, 4 })
        {
            var cell = row.Cells[index];
            cell.Borders.Width = Unit.FromPoint(0.5);
            cell.Borders.Color = ClrBorder;
            cell.Borders.Visible = true;
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Format.LeftIndent = Unit.FromPoint(4);
            cell.Format.RightIndent = Unit.FromPoint(4);
            cell.Format.Font.Size = Unit.FromPoint(grandTotal ? 10 : 8);
            if (grandTotal)
            {
                cell.Shading.Color = ClrHeaderBg;
                cell.Format.Font.Color = Colors.White;
                cell.Format.Font.Bold = true;
            }
        }

        row.Cells[2].Format.Font.Bold = true;
        if (!grandTotal)
            row.Cells[2].Shading.Color = ClrBandBg;
        row.Cells[2].AddParagraph(label);
        row.Cells[4].Format.Alignment = ParagraphAlignment.Right;
        row.Cells[4].AddParagraph(value);
    }

    private static void BuildVatCastellettoRows(Table table, InvoicePdfModel model)
    {
        AddVatRow(table, ["Aliquota / Natura", "Imponibile", "Imposta", "Esigibilità"], heading: true);

        foreach (var vat in model.VatSummary)
        {
            var exempt = !string.IsNullOrWhiteSpace(vat.ExemptionNature);
            var rate = vat.VatRate.ToString("0.##", ItalianCulture);
            AddVatRow(table,
            [
                exempt ? $"{rate}%\n{vat.ExemptionNature}" : $"{rate}%\n(Ordinaria)",
                Money(vat.TaxableAmount, model.Currency),
                Money(vat.VatAmount, model.Currency),
                exempt ? "-" : "I (Immediata)"
            ]);
        }
    }

    private static void AddVatRow(Table table, string[] values, bool heading = false)
    {
        var row = table.AddRow();
        for (var i = 0; i < values.Length; i++)
        {
            var cell = row.Cells[i + 2];
            cell.Borders.Width = Unit.FromPoint(0.5);
            cell.Borders.Color = ClrBorder;
            cell.Borders.Visible = true;
            cell.Format.Font.Size = Unit.FromPoint(7);
            cell.Format.LeftIndent = Unit.FromPoint(3);
            cell.Format.RightIndent = Unit.FromPoint(3);
            cell.Format.Alignment = !heading && i is 1 or 2
                ? ParagraphAlignment.Right
                : ParagraphAlignment.Left;
            if (heading)
            {
                cell.Shading.Color = ClrBandBg;
                cell.Format.Font.Bold = true;
            }

            cell.AddParagraph(values[i]);
        }
    }

    // ─── Small helpers ────────────────────────────────────────────────────────

    private static void AddCellSectionLabel(Cell cell, string text)
    {
        var p = cell.AddParagraph(text);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = Unit.FromPoint(7);
        p.Format.SpaceAfter = Unit.FromPoint(3);
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

    private static void AddVerticalSpacer(Section section, double points)
    {
        var p = section.AddParagraph();
        p.Format.Font.Size = Unit.FromPoint(1);
        p.Format.LineSpacingRule = LineSpacingRule.Exactly;
        p.Format.LineSpacing = Unit.FromPoint(points);
        p.Format.SpaceBefore = Unit.FromPoint(0);
        p.Format.SpaceAfter = Unit.FromPoint(0);
    }

    private static string Money(decimal amount, string currency) =>
        $"{(currency == "EUR" ? "€" : currency)} {amount.ToString("N2", ItalianCulture)}";

    private static string? BuildRegistryLine(InvoicePdfSellerDetails seller)
    {
        if (string.IsNullOrWhiteSpace(seller.Rea) && string.IsNullOrWhiteSpace(seller.ReaOffice))
            return null;

        var office = string.IsNullOrWhiteSpace(seller.ReaOffice) ? null : $" di {seller.ReaOffice}";
        var rea = string.IsNullOrWhiteSpace(seller.Rea) ? null : $"  -  REA {seller.Rea}";
        return $"Iscritta al Reg. Imprese{office}{rea}";
    }

    private static string? BuildShareCapitalLine(InvoicePdfSellerDetails seller)
    {
        if (string.IsNullOrWhiteSpace(seller.ShareCapital))
            return null;

        var paymentStatus = seller.IsShareCapitalFullyPaid ? " i.v." : " deliberato, non interamente versato";
        return $"Capitale sociale € {seller.ShareCapital}{paymentStatus}";
    }

    private static string? BuildLegalStatusLine(InvoicePdfSellerDetails seller)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(seller.LegalForm))
            parts.Add(seller.LegalForm);
        if (seller.IsSoleShareholder == true)
            parts.Add("Soci: socio unico");
        else if (seller.IsSoleShareholder == false)
            parts.Add("Soci: pluralità di soci");
        if (seller.IsInLiquidation is { } isInLiquidation)
            parts.Add(isInLiquidation ? "in liquidazione" : "non in liquidazione");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? BuildFooterLegalStatus(InvoicePdfSellerDetails seller)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(seller.LegalForm))
            parts.Add("S.r.l.");
        if (seller.IsSoleShareholder.HasValue)
            parts.Add(seller.IsSoleShareholder.Value ? "socio unico" : "soci plurimi");
        if (seller.IsInLiquidation is { } isInLiquidation)
            parts.Add(isInLiquidation ? "in liquidazione" : "non in liquidazione");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? BuildFooterRegistryLine(InvoicePdfSellerDetails seller)
    {
        if (string.IsNullOrWhiteSpace(seller.Rea) && string.IsNullOrWhiteSpace(seller.ReaOffice))
            return null;

        var registryOffice = string.IsNullOrWhiteSpace(seller.ReaOffice)
            ? null
            : $"Reg. Imprese {seller.ReaOffice}";
        var rea = string.IsNullOrWhiteSpace(seller.Rea) ? null : $"REA {seller.Rea}";
        return string.Join(", ", new[] { registryOffice, rea }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int GetSizeSortOrder(string? size) =>
        size?.Trim().ToUpperInvariant() switch
        {
            "XXXS" => 0,
            "XXS" => 1,
            "XS" => 2,
            "S" => 3,
            "M" => 4,
            "L" => 5,
            "XL" => 6,
            "XXL" or "2XL" => 7,
            "XXXL" or "3XL" => 8,
            "XXXXL" or "4XL" => 9,
            _ => 10
        };

    private static void BuildPaymentSummary(Cell cell, InvoicePdfModel model)
    {
        if (model.Payment.DueDate is null && model.Payment.Amount is null &&
            string.IsNullOrWhiteSpace(model.Payment.PaymentTerms))
            return;

        var table = new Table();
        table.AddColumn(Unit.FromCentimeter(5.4));
        table.AddColumn(Unit.FromCentimeter(3.6));
        table.Borders.Width = Unit.FromPoint(0.35);
        table.Borders.Color = ClrBorder;
        table.Format.Font.Size = Unit.FromPoint(7);
        table.TopPadding = Unit.FromPoint(2);
        table.BottomPadding = Unit.FromPoint(2);

        AddPaymentSummaryRow(table, "Modalità di pagamento",
            $"{model.Payment.PaymentMethod}  -  {model.Payment.PaymentTerms}");
        if (model.Payment.DueDate is { } dueDate)
            AddPaymentSummaryRow(table, "Data scadenza rata / quietanza",
                dueDate.ToString("dd/MM/yyyy", ItalianCulture));
        if (model.Payment.Amount is { } amount)
            AddPaymentSummaryRow(table, "Importo da pagare", Money(amount, model.Currency), rightAlign: true);

        cell.Elements.Add(table);
    }

    private static void AddPaymentSummaryRow(Table table, string label, string value, bool rightAlign = false)
    {
        var row = table.AddRow();
        row.Cells[0].Shading.Color = ClrBandBg;
        row.Cells[0].Format.Font.Bold = true;
        row.Cells[0].AddParagraph(label);
        row.Cells[1].Format.Alignment = rightAlign ? ParagraphAlignment.Right : ParagraphAlignment.Left;
        row.Cells[1].AddParagraph(value);
    }

    private static bool IsFiscalDocument(InvoicePdfModel model) =>
        IsInvoice(model) || model.DocumentType.Equals("NOTA CREDITO", StringComparison.OrdinalIgnoreCase);
}