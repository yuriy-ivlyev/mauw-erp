using Mauw.Erp.Application;
using Mauw.Erp.Infrastructure.Reporting;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IPdfGenerator, QuestPdfInvoiceGenerator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/api/v1/invoices/{id}/pdf", (string id, IPdfGenerator pdfGenerator) =>
{
    var invoice = new InvoiceReportDto
    {
        DocumentNumber = $"FA-{id}",
        IssueDate = DateTime.Today,
        CustomerName = "Cliente demo",
        CustomerAddress = "Via Roma 1, Milano",
        CustomerVatNumber = "IT01234567890",
        TaxableAmount = 150m,
        TotalTax = 33m,
        TotalDue = 183m,
        VatSummary =
        [
            new VatSummaryDto { RateLabel = "22%", TaxableAmount = 150m, TaxAmount = 33m, Total = 183m },
        ],
        Lines =
        [
            new InvoiceLineDto { Code = "ITEM-001", Description = "Prodotto demo", SizeColor = "M / Nero", Quantity = 3, UnitPrice = 50m, Total = 150m },
        ],
    };

    var pdfBytes = pdfGenerator.GenerateInvoicePdf(invoice);
    return Results.File(pdfBytes, "application/pdf", fileDownloadName: $"invoice-{id}.pdf");
});

app.Run();
