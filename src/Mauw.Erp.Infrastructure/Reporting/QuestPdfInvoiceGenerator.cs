using System.Globalization;
using Mauw.Erp.Application;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Mauw.Erp.Infrastructure.Reporting;

public sealed class QuestPdfInvoiceGenerator : IPdfGenerator
{
    static QuestPdfInvoiceGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateInvoicePdf(InvoiceReportDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Helvetica"));

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(container => DrawInvoiceHeader(container, dto));
                    column.Item().Element(container => DrawCustomerBox(container, dto.CustomerName, dto.CustomerAddress, dto.CustomerVatNumber));
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(2.8f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.0f);
                            columns.RelativeColumn(1.0f);
                            columns.RelativeColumn(1.2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Codice").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).Text("Descrizione").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Taglia/Colore").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Quantità").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Prezzo unitario").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Totale riga").FontColor(Colors.White).Bold();
                        });

                        for (var index = 0; index < dto.Lines.Count; index++)
                        {
                            var line = dto.Lines[index];
                            var shade = index % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                            table.Cell().Background(shade).Padding(4).Text(line.Code);
                            table.Cell().Background(shade).Padding(4).Text(line.Description);
                            table.Cell().Background(shade).Padding(4).AlignCenter().Text(line.SizeColor);
                            table.Cell().Background(shade).Padding(4).AlignRight().Text(FormatQuantity(line.Quantity));
                            table.Cell().Background(shade).Padding(4).AlignRight().Text(FormatMoney(line.UnitPrice));
                            table.Cell().Background(shade).Padding(4).AlignRight().Text(FormatMoney(line.Total));
                        }
                    });

                    column.Item().Element(container => DrawInvoiceSummary(container, dto));
                    column.Item().Text(dto.Notes ?? "");
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Dichiarazione di cortesia art. 21 DPR 633/72").FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.Span(" - Pagina ").FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.Span(" di ").FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.TotalPages().FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateDdtPdf(DdtReportDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Helvetica"));

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(container => DrawDdtHeader(container, dto));
                    column.Item().Element(container => DrawCustomerBox(container, dto.CustomerName, dto.CustomerAddress, dto.CustomerVatNumber));
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(2.8f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.0f);
                            columns.RelativeColumn(1.0f);
                            columns.RelativeColumn(1.2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Codice").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).Text("Descrizione").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Taglia/Colore").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Quantità").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Prezzo unitario").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter().Text("Totale riga").FontColor(Colors.White).Bold();
                        });

                        for (var index = 0; index < dto.Lines.Count; index++)
                        {
                            var line = dto.Lines[index];
                            var shade = index % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                            table.Cell().Background(shade).Padding(4).Text(line.Code);
                            table.Cell().Background(shade).Padding(4).Text(line.Description);
                            table.Cell().Background(shade).Padding(4).AlignCenter().Text(line.SizeColor);
                            table.Cell().Background(shade).Padding(4).AlignRight().Text(FormatQuantity(line.Quantity));
                            table.Cell().Background(shade).Padding(4).AlignRight().Text(FormatMoney(line.UnitPrice));
                            table.Cell().Background(shade).Padding(4).AlignRight().Text(FormatMoney(line.Total));
                        }
                    });

                    column.Item().Element(container => DrawDdtSummary(container, dto));
                    column.Item().Text(dto.Notes ?? "");
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Dichiarazione di cortesia art. 21 DPR 633/72").FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.Span(" - Pagina ").FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.Span(" di ").FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.TotalPages().FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }

    private static void DrawInvoiceHeader(IContainer container, InvoiceReportDto dto)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                if (dto.LogoBytes is { Length: > 0 })
                {
                    column.Item().AlignCenter().Width(140).Height(60).Image(dto.LogoBytes);
                }
                else
                {
                    column.Item().AlignCenter().Width(140).Height(60).Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Text("Logo");
                }
            });

            row.RelativeItem().AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text("FATTURA").Bold().FontSize(18).FontColor(Colors.BlueGrey.Darken4);
                column.Item().AlignRight().Text($"N. {dto.DocumentNumber}");
                column.Item().AlignRight().Text($"Data: {dto.IssueDate:dd/MM/yyyy}");
            });
        });
    }

    private static void DrawDdtHeader(IContainer container, DdtReportDto dto)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                if (dto.LogoBytes is { Length: > 0 })
                {
                    column.Item().AlignCenter().Width(140).Height(60).Image(dto.LogoBytes);
                }
                else
                {
                    column.Item().AlignCenter().Width(140).Height(60).Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Text("Logo");
                }
            });

            row.RelativeItem().AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text("DDT").Bold().FontSize(18).FontColor(Colors.BlueGrey.Darken4);
                column.Item().AlignRight().Text($"N. {dto.DocumentNumber}");
                column.Item().AlignRight().Text($"Data: {dto.IssueDate:dd/MM/yyyy}");
            });
        });
    }

    private static void DrawCustomerBox(IContainer container, string customerName, string customerAddress, string customerVatNumber)
    {
        container.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Text("Cliente").Bold().FontColor(Colors.BlueGrey.Darken4);
            column.Item().Text(customerName);
            column.Item().Text(customerAddress);
            column.Item().Text($"P. IVA: {customerVatNumber}");
        });
    }

    private static void DrawInvoiceSummary(IContainer container, InvoiceReportDto dto)
    {
        container.Width(260).AlignRight().Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Imponibile");
                row.ConstantItem(100).AlignRight().Text(FormatMoney(dto.TaxableAmount));
            });

            foreach (var vat in dto.VatSummary)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Castelletto IVA {vat.RateLabel}");
                    row.ConstantItem(100).AlignRight().Text(FormatMoney(vat.TaxAmount));
                });
            }

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Totale dovuto").Bold();
                row.ConstantItem(100).AlignRight().Text(FormatMoney(dto.TotalDue)).Bold();
            });
        });
    }

    private static void DrawDdtSummary(IContainer container, DdtReportDto dto)
    {
        container.Width(260).AlignRight().Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Imponibile");
                row.ConstantItem(100).AlignRight().Text(FormatMoney(dto.TaxableAmount));
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Totale dovuto").Bold();
                row.ConstantItem(100).AlignRight().Text(FormatMoney(dto.TotalDue)).Bold();
            });
        });
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("C", CultureInfo.GetCultureInfo("it-IT"));
    }

    private static string FormatQuantity(decimal value)
    {
        return value.ToString("0.##");
    }

}
