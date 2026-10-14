using Mauw.Erp.Application.Validators;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Invoice management endpoints for creating invoices and processing payments.
/// </summary>
public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/invoices")
            .WithName("Invoices")
            .WithOpenApi();

        group.MapPost("/", CreateInvoice)
            .WithName("CreateInvoice")
            .WithSummary("Create a new invoice");

        group.MapGet("/{id}", GetInvoice)
            .WithName("GetInvoice")
            .WithSummary("Get invoice details by ID");

        group.MapGet("/", ListInvoices)
            .WithName("ListInvoices")
            .WithSummary("List invoices with pagination");

        group.MapPost("/{id}/payments", RecordPayment)
            .WithName("RecordPayment")
            .WithSummary("Record a payment on an invoice (triggers commission accrual when paid)");

        group.MapGet("/{id}/pdf", GetInvoicePdf)
            .WithName("GetInvoicePdf")
            .WithSummary("Generate and download invoice PDF");
    }

    private static async Task<IResult> CreateInvoice(
        CreateInvoiceRequest request,
        MauwDbContext dbContext,
        CreateInvoiceRequestValidator validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.Errors);

        // Get the contact to generate proper invoice number
        var contact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == request.ContactId);
        if (contact == null)
            return Results.NotFound("Contact not found");

        // Generate invoice number (simple format: INV-YYYYMMDD-XXXX)
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        // Create invoice
        var invoice = new Invoice(
            contactId: request.ContactId,
            invoiceNumber: invoiceNumber);

        // Create agent commission if contact has an agent
        if (contact.AgentId.HasValue)
        {
            var commission = new AgentCommission(
                agentId: contact.AgentId.Value,
                invoiceId: invoice.Id,
                taxableAmountBase: 0, // Will be updated when items are added
                commissionRate: contact.DefaultCommissionRate);

            invoice.Commissions.Add(commission);
        }

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        return Results.Created($"/api/v1/invoices/{invoice.Id}", new InvoiceDto(
            invoice.Id,
            invoice.ContactId,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.TaxableTotal,
            invoice.VatTotal,
            invoice.GrossTotal,
            invoice.PaidAmount,
            []
        ));
    }

    private static async Task<IResult> GetInvoice(
        Guid id,
        MauwDbContext dbContext)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return Results.NotFound();

        var items = invoice.Items.Select(i => new InvoiceItemDto(
            i.Id,
            i.VariantId,
            i.Quantity,
            i.UnitNetPrice,
            i.LineNetTotal,
            i.VatRate,
            i.LineVatTotal,
            i.LineGrossTotal
        )).ToList();

        return Results.Ok(new InvoiceDto(
            invoice.Id,
            invoice.ContactId,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.TaxableTotal,
            invoice.VatTotal,
            invoice.GrossTotal,
            invoice.PaidAmount,
            items
        ));
    }

    private static async Task<IResult> ListInvoices(
        int page = 1,
        int pageSize = 10,
        MauwDbContext dbContext = null!)
    {
        const int maxPageSize = 100;
        if (pageSize > maxPageSize)
            pageSize = maxPageSize;

        var totalCount = await dbContext.Invoices.CountAsync();
        var invoices = await dbContext.Invoices
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(i => i.Items)
            .ToListAsync();

        var items = invoices.Select(i => new InvoiceDto(
            i.Id,
            i.ContactId,
            i.InvoiceNumber,
            i.InvoiceDate,
            i.TaxableTotal,
            i.VatTotal,
            i.GrossTotal,
            i.PaidAmount,
            []
        )).ToList();

        return Results.Ok(new
        {
            items,
            pageInfo = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (decimal)pageSize)
            }
        });
    }

    private static async Task<IResult> RecordPayment(
        Guid id,
        RecordPaymentRequest request,
        MauwDbContext dbContext)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Commissions)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return Results.NotFound("Invoice not found");

        if (request.Amount <= 0)
            return Results.BadRequest("Payment amount must be greater than zero");

        if (invoice.PaidAmount + request.Amount > invoice.GrossTotal)
            return Results.BadRequest("Payment amount exceeds invoice total");

        // Record payment
        var payment = new Payment(
            invoiceId: invoice.Id,
            amount: request.Amount,
            paymentDate: DateTime.UtcNow);

        invoice.Payments.Add(payment);
        invoice.PaidAmount += request.Amount;

        // Check if invoice is fully paid and update commission status
        if (invoice.PaidAmount >= invoice.GrossTotal)
        {
            foreach (var commission in invoice.Commissions)
            {
                if (commission.Status == CommissionStatus.Pending)
                    commission.Status = CommissionStatus.Accrued;
            }
        }

        await dbContext.SaveChangesAsync();

        return Results.Ok(new { PaymentRecorded = true, TotalPaid = invoice.PaidAmount });
    }

    private static async Task<IResult> GetInvoicePdf(
        Guid id,
        MauwDbContext dbContext,
        IPdfGenerator pdfGenerator)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Items)
            .ThenInclude(item => item.Variant)
            .Include(i => i.Contact)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return Results.NotFound();

        try
        {
            // Group VAT by rate for breakdown
            var vatBreakdown = invoice.Items
                .GroupBy(i => i.VatRate)
                .Select(g => new VatBreakdownReportDto(
                    g.Key,
                    g.Sum(i => i.LineNetTotal),
                    g.Sum(i => i.LineVatTotal)
                ))
                .ToList();

            // Create minimal report DTO
            var reportDto = new InvoiceReportDto(
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                new ContactReportDto(
                    invoice.Contact.BusinessName,
                    invoice.Contact.VatNumber,
                    invoice.Contact.SdiCode),
                null,
                invoice.Items.Select(i => new InvoiceReportLineDto(
                    i.Id.ToString(),
                    i.Id.ToString(),
                    null,
                    i.Quantity,
                    i.UnitNetPrice,
                    i.LineNetTotal
                )).ToList(),
                new InvoiceTotalsDto(
                    invoice.TaxableTotal,
                    vatBreakdown,
                    invoice.VatTotal,
                    invoice.GrossTotal
                ),
                null
            );

            var pdfBytes = pdfGenerator.GenerateInvoicePdf(reportDto);
            return Results.File(pdfBytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
        }
        catch (Exception ex)
        {
            return Results.BadRequest($"PDF generation failed: {ex.Message}");
        }
    }
}
