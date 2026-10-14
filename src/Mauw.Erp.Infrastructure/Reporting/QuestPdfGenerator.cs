using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using Mauw.Erp.Contracts.Dtos;

namespace Mauw.Erp.Infrastructure.Reporting;

/// <summary>
/// Interface for PDF report generation.
/// </summary>
public interface IPdfGenerator
{
    byte[] GenerateInvoicePdf(InvoiceReportDto dto);
    byte[] GenerateDdtPdf(DdtReportDto dto);
}

/// <summary>
/// QuestPDF implementation of PDF generator for Italian invoices and DDT documents.
/// </summary>
public class QuestPdfGenerator : IPdfGenerator
{
    public QuestPdfGenerator()
    {
        // Configure QuestPDF license for Community (free) use
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateInvoicePdf(InvoiceReportDto dto)
    {
        try
        {
            return Document
                .Create(document => document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.Header().Element(h => ComposeInvoiceHeader(h, dto));
                    page.Content().Element(c => ComposeInvoiceContent(c, dto));
                    page.Footer().Element(f => ComposeFooter(f));
                }))
                .GeneratePdf();
        }
        catch (Exception)
        {
            // Return a placeholder PDF if generation fails
            return GeneratePlaceholderPdf($"Invoice {dto.InvoiceNumber}");
        }
    }

    public byte[] GenerateDdtPdf(DdtReportDto dto)
    {
        try
        {
            return Document
                .Create(document => document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.Header().Element(h => ComposeDdtHeader(h, dto));
                    page.Content().Element(c => ComposeDdtContent(c, dto));
                    page.Footer().Element(f => ComposeFooter(f));
                }))
                .GeneratePdf();
        }
        catch (Exception)
        {
            // Return a placeholder PDF if generation fails
            return GeneratePlaceholderPdf($"DDT {dto.DdtNumber}");
        }
    }

    private static void ComposeInvoiceHeader(IContainer container, InvoiceReportDto dto)
    {
        container.Column(column =>
        {
            column.Item().Text("FATTURA").FontSize(24).Bold();
            column.Item().Text($"N. {dto.InvoiceNumber}").FontSize(14).Bold();
            column.Item().Text($"Data: {dto.InvoiceDate:dd/MM/yyyy}").FontSize(12);
            column.Spacing(10);

            column.Item().Row(row =>
            {
                row.RelativeColumn().Column(col =>
                {
                    col.Item().Text("CLIENTE").FontSize(12).Bold();
                    col.Item().Text(dto.Customer.BusinessName).FontSize(11);
                    col.Item().Text($"P.IVA: {dto.Customer.VatNumber}").FontSize(10);
                });

                row.RelativeColumn().Column(col =>
                {
                    col.Item().Text("SOCIETÀ").FontSize(12).Bold();
                    if (dto.Company != null)
                    {
                        col.Item().Text(dto.Company.BusinessName).FontSize(11);
                        col.Item().Text($"P.IVA: {dto.Company.VatNumber}").FontSize(10);
                    }
                });
            });
        });
    }

    private static void ComposeInvoiceContent(IContainer container, InvoiceReportDto dto)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            // Simple text content for lines
            column.Item().Text("DETTAGLIO RIGHE").Bold().FontSize(12);
            
            foreach (var line in dto.Lines)
            {
                column.Item().Text($"{line.Codice} - {line.Descrizione}: {line.Quantita} x €{line.PrezzoUnitario:0.00} = €{line.Totale:0.00}");
            }

            column.Spacing(15);

            // Totals
            column.Item().Column(col =>
            {
                col.Item().Text($"Imponibile: €{dto.Totals.TaxableAmount:0.00}").Bold();
                col.Item().Text($"IVA: €{dto.Totals.TotalVat:0.00}").Bold();
                col.Item().Text($"TOTALE: €{dto.Totals.TotalDue:0.00}").Bold().FontSize(14);
            });
        });
    }

    private static void ComposeDdtHeader(IContainer container, DdtReportDto dto)
    {
        container.Column(column =>
        {
            column.Item().Text("DOCUMENTO DI TRASPORTO (DDT)").FontSize(24).Bold();
            column.Item().Text($"N. {dto.DdtNumber}").FontSize(14).Bold();
            column.Item().Text($"Data: {dto.DdtDate:dd/MM/yyyy}").FontSize(12);
            column.Spacing(10);

            column.Item().Row(row =>
            {
                row.RelativeColumn().Column(col =>
                {
                    col.Item().Text("CLIENTE").FontSize(12).Bold();
                    col.Item().Text(dto.Customer.BusinessName).FontSize(11);
                    col.Item().Text($"P.IVA: {dto.Customer.VatNumber}").FontSize(10);
                });

                row.RelativeColumn().Column(col =>
                {
                    col.Item().Text("SOCIETÀ").FontSize(12).Bold();
                    if (dto.Company != null)
                    {
                        col.Item().Text(dto.Company.BusinessName).FontSize(11);
                    }
                });
            });
        });
    }

    private static void ComposeDdtContent(IContainer container, DdtReportDto dto)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Text("DETTAGLIO MERCI").Bold().FontSize(12);
            
            foreach (var line in dto.Lines)
            {
                column.Item().Text($"{line.Codice} - {line.Descrizione}: {line.Quantita} x €{line.PrezzoUnitario:0.00} = €{line.Totale:0.00}");
            }

            column.Spacing(15);

            column.Item().Column(col =>
            {
                col.Item().Text($"Totale Netto: €{dto.Totals.TaxableAmount:0.00}").Bold();
                col.Item().Text($"TOTALE DDT: €{dto.Totals.TotalDue:0.00}").Bold().FontSize(14);
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Copia analogica di fattura elettronica ai sensi dell'art. 21 DPR 633/72").FontSize(8);
            column.Item().Text($"Generato il: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8);
        });
    }

    private static byte[] GeneratePlaceholderPdf(string title)
    {
        return Document
            .Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);

                    page.Content().Column(col =>
                    {
                        col.Item().Text(title).FontSize(24).Bold();
                        col.Item().Text("PDF generation placeholder").FontSize(12);
                    });
                });
            })
            .GeneratePdf();
    }
}
