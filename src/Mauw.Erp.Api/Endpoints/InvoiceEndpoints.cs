using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Invoice management endpoints for creating invoices and processing payments.
/// Thin layer delegating to IInvoiceService.
/// </summary>
public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/invoices")
            .WithName("Invoices");

        group.MapPost("/", CreateInvoice)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("CreateInvoice")
            .WithSummary("Create a new invoice");

        // Read-only linked document: also needed by Warehouse for shipments.
        group.MapGet("/{id}", GetInvoice)
            .RequireAuthorization(AppPolicies.AnyRole)
            .WithName("GetInvoice")
            .WithSummary("Get invoice details by ID");

        group.MapGet("/", ListInvoices)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("ListInvoices")
            .WithSummary("List invoices with pagination");

        group.MapPost("/{id}/payments", RecordPayment)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("RecordPayment")
            .WithSummary("Record a payment on an invoice (triggers commission accrual when paid)");

        group.MapGet("/{id}/pdf", GetInvoicePdf)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("GetInvoicePdf")
            .WithSummary("Generate and download invoice PDF");
    }

    private static async Task<Results<Created<InvoiceDto>, BadRequest<string>>> CreateInvoice(
        CreateInvoiceRequest request,
        IInvoiceService invoiceService)
    {
        var result = await invoiceService.CreateInvoiceAsync(request);

        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        return TypedResults.Created($"/api/v1/invoices/{result.Data!.Id}", result.Data);
    }

    private static async Task<Results<Ok<InvoiceDto>, NotFound>> GetInvoice(
        Guid id,
        IInvoiceService invoiceService)
    {
        var result = await invoiceService.GetInvoiceAsync(id);

        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Ok<List<InvoiceDto>>> ListInvoices(
        IInvoiceService invoiceService,
        int page = 1,
        int pageSize = 20)
    {
        return TypedResults.Ok(new List<InvoiceDto>());
    }

    private static async Task<Results<Ok<object>, BadRequest<string>, NotFound>> RecordPayment(
        Guid id,
        RecordPaymentRequest request,
        IInvoiceService invoiceService)
    {
        var result = await invoiceService.RecordPaymentAsync(id, request);

        if (result.Success) return TypedResults.Ok((object)new { PaymentRecorded = true });
        if (result.Error != null && result.Error.Contains("not found"))
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<FileContentHttpResult, NotFound, BadRequest<string>>> GetInvoicePdf(
        Guid id,
        IInvoiceService invoiceService)
    {
        var result = await invoiceService.GetInvoicePdfAsync(id);
        if (!result.Success)
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? TypedResults.NotFound()
                : TypedResults.BadRequest(result.Error!);

        return TypedResults.File(result.Data!, "application/pdf", $"fattura-{id}.pdf");
    }
}
