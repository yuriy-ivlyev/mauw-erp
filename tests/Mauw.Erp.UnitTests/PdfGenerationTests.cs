using Mauw.Erp.Application;
using Mauw.Erp.Infrastructure.Reporting;

namespace Mauw.Erp.UnitTests;

public class PdfGenerationTests
{
    [Fact]
    public void GenerateInvoicePdf_ShouldCreatePdfBytes()
    {
        var generator = new QuestPdfInvoiceGenerator();

        var dto = new InvoiceReportDto
        {
            CustomerName = "Acme S.r.l.",
            CustomerAddress = "Via Roma 12, Milano",
            CustomerVatNumber = "IT12345678901",
            TaxableAmount = 100m,
            TotalTax = 22m,
            TotalDue = 122m,
            Lines =
            [
                new InvoiceLineDto { Code = "A-001", Description = "Prodotto A", SizeColor = "M / Nero", Quantity = 2, UnitPrice = 50m, Total = 100m },
            ],
            VatSummary =
            [
                new VatSummaryDto { RateLabel = "22%", TaxableAmount = 100m, TaxAmount = 22m, Total = 122m },
            ],
        };

        var pdf = generator.GenerateInvoicePdf(dto);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Take(4).ToArray()));
    }

    [Fact]
    public void GenerateDdtPdf_ShouldCreatePdfBytes()
    {
        var generator = new QuestPdfInvoiceGenerator();

        var dto = new DdtReportDto
        {
            CustomerName = "Acme S.r.l.",
            CustomerAddress = "Via Roma 12, Milano",
            CustomerVatNumber = "IT12345678901",
            TaxableAmount = 100m,
            TotalDue = 100m,
            Lines =
            [
                new DdtLineDto { Code = "A-001", Description = "Prodotto A", SizeColor = "M / Nero", Quantity = 1, UnitPrice = 100m, Total = 100m },
            ],
        };

        var pdf = generator.GenerateDdtPdf(dto);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Take(4).ToArray()));
    }
}
