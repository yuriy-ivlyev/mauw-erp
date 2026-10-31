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

        group.MapPost("/from-order/{orderId:guid}", GenerateFromOrder)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("GenerateInvoiceFromOrder")
            .WithSummary("Generate a final invoice from a completed order");

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

        group.MapGet("/{id}/xml", GetInvoiceXml)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("GetInvoiceXml")
            .WithSummary("Generate and download the FatturaPA XML of an invoice");
    }

    private static async Task<Results<Created<InvoiceDto>, BadRequest<string>, NotFound, Conflict<string>>> GenerateFromOrder(
        Guid orderId,
        IInvoiceService invoiceService)
    {
        var result = await invoiceService.GenerateFromOrderAsync(orderId);

        if (!result.Success &&
            (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true ||
             result.Error?.Contains("non trovato", StringComparison.OrdinalIgnoreCase) == true))
            return TypedResults.NotFound();
        if (!result.Success && result.Error?.Contains("già", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.Conflict(result.Error);
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

    private static async Task<Results<Ok<PagedResult<InvoiceDto>>, BadRequest<string>>> ListInvoices(
        IInvoiceService invoiceService,
        Guid? contactId = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        bool unpaidOnly = false,
        string? sortBy = null,
        bool sortDescending = true)
    {
        var result = await invoiceService.GetInvoicesAsync(
            contactId, search, page, pageSize, status, dateFrom, dateTo, unpaidOnly, sortBy, sortDescending);
        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<InvoiceDto>, BadRequest<string>, NotFound>> RecordPayment(
        Guid id,
        RecordPaymentRequest request,
        IInvoiceService invoiceService)
    {
        var result = await invoiceService.RecordPaymentAsync(id, request);

        if (!result.Success && result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.NotFound();
        if (!result.Success)
            return TypedResults.BadRequest(result.Error);
        return TypedResults.Ok(result.Data!);
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

    private static async Task<Results<FileContentHttpResult, NotFound, BadRequest<string>>> GetInvoiceXml(
        Guid id,
        IInvoiceService invoiceService)
    {
        var result = await invoiceService.GetInvoiceXmlAsync(id);
        if (!result.Success)
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? TypedResults.NotFound()
                : TypedResults.BadRequest(result.Error!);

        return TypedResults.File(
            System.Text.Encoding.UTF8.GetBytes(result.Data!), "application/xml", $"fattura-{id}.xml");
    }
}
