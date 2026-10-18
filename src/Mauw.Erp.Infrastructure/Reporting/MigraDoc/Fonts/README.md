# PDF fonts

PDF reporting embeds Noto Sans (regular, bold, italic and bold italic), so rendering
works on Windows and Linux containers without installing system fonts.
The font resolver also maps MigraDoc's predefined fonts, including `Courier New`,
to these embedded resources.

Source: https://github.com/notofonts/noto-fonts/tree/main/hinted/ttf/NotoSans

The font files are redistributed under the SIL Open Font License 1.1; see `LICENSE`.
Configure reporting with `AddInvoicePdfReporting()` before creating any PDF fonts.

PDFsharp-MigraDoc is pinned to 6.2.4 (MIT). Version 6.5 introduces AGPL/commercial
licensing and requires explicit license configuration; do not upgrade without
reviewing those requirements.

## Layout checks

The full seller/customer heading is normal first-page content. Only the compact
document/customer bar is a repeated header. Keep this separation so longer
addresses push the items down instead of overlapping them.

Document details, totals and the VAT breakdown use table columns, not tab stops.
Amounts use Italian formatting independently of the server's current culture.
Unit prices retain 2-8 decimal places; monetary line totals and summaries use 2.
VAT and document totals must come from `ItalianAccountingCalculator`, grouped by
rate and Nature. Never sum per-line VAT or derive a rate from rounded tax.
The summary is kept together and all item pages repeat the column headings.

`InvoicePdfGeneratorTests` checks text coordinates with PdfPig (test-only),
including overlaps, clipping, missing/duplicate items, repeated headings and
short/long documents. To export samples for visual inspection, set
`PDF_LAYOUT_OUTPUT` to an output directory before running these tests.
