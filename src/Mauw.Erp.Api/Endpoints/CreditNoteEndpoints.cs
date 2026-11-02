using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Commands.CreditNotes;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

public static class CreditNoteEndpoints
{
    public static void MapCreditNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/credit-notes")
            .WithName("CreditNotes")
            .RequireAuthorization(AppPolicies.Billing);

        group.MapPost("/preview", Preview)
            .WithName("PreviewCreditNote")
            .WithSummary("Calculate the credit-note totals from an eligible invoice");

        group.MapPost("/", Create)
            .WithName("CreateCreditNote")
            .WithSummary("Issue a credit note against a transmitted invoice");

        group.MapGet("/{id:guid}", GetCreditNote)
            .WithName("GetCreditNote")
            .WithSummary("Get a credit note and its fiscal lines");

        group.MapGet("/{id:guid}/xml", GetCreditNoteXml)
            .WithName("GetCreditNoteXml")
            .WithSummary("Download the frozen or on-demand TD04 XML");

        group.MapPost("/{id:guid}/transmit-sdi", TransmitCreditNoteToSdi)
            .WithName("TransmitCreditNoteToSdi")
            .WithSummary("Validate, archive and transmit a credit note to the SDI");
    }

    private static async Task<Results<Ok<CreditNotePreviewDto>, BadRequest<string>, NotFound>> Preview(
        CreateCreditNoteRequest request,
        ICreditNoteService creditNoteService,
        CancellationToken cancellationToken)
    {
        var result = await creditNoteService.PreviewAsync(request, cancellationToken);
        if (!result.Success)
            return result.Error?.Contains("non trovata", StringComparison.OrdinalIgnoreCase) == true
                ? TypedResults.NotFound()
                : TypedResults.BadRequest(result.Error ?? "Anteprima nota di credito non riuscita.");

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Created<CreditNoteIssuedDto>, BadRequest<string>, NotFound, Conflict<string>>> Create(
        CreateCreditNoteRequest request,
        MediatR.ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateCreditNoteCommand(request), cancellationToken);
        if (!result.Success)
        {
            if (result.Error?.Contains("non trovata", StringComparison.OrdinalIgnoreCase) == true)
                return TypedResults.NotFound();
            if (result.Error?.Contains("SDI", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.Contains("supera il residuo", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.Contains("non contigua", StringComparison.OrdinalIgnoreCase) == true)
                return TypedResults.Conflict(result.Error);
            return TypedResults.BadRequest(result.Error ?? "Emissione nota di credito non riuscita.");
        }

        return TypedResults.Created(
            $"/api/v1/credit-notes/{result.Data!.CreditNote.Id}", result.Data);
    }

    private static async Task<Results<Ok<CreditNoteDetailDto>, NotFound, BadRequest<string>>> GetCreditNote(
        Guid id,
        ICreditNoteService creditNoteService,
        CancellationToken cancellationToken)
    {
        var result = await creditNoteService.GetByIdAsync(id, cancellationToken);
        if (!result.Success)
            return result.Error?.Contains("non trovata", StringComparison.OrdinalIgnoreCase) == true
                ? TypedResults.NotFound()
                : TypedResults.BadRequest(result.Error ?? "Impossibile caricare la nota di credito.");

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<FileContentHttpResult, NotFound, BadRequest<string>>> GetCreditNoteXml(
        Guid id,
        IInvoiceService invoiceService)
    {
        var result = await invoiceService.GetCreditNoteXmlDownloadAsync(id);
        if (!result.Success)
            return result.Error?.Contains("non trovata", StringComparison.OrdinalIgnoreCase) == true
                ? TypedResults.NotFound()
                : TypedResults.BadRequest(result.Error!);

        return TypedResults.File(result.Data!.Content, "application/xml", result.Data.FileName);
    }

    private static async Task<Results<Ok<CreditNoteSummaryDto>, NotFound, BadRequest<string>, Conflict<string>>>
        TransmitCreditNoteToSdi(
            Guid id,
            MediatR.ISender sender,
            CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TransmitCreditNoteToSdiCommand(id), cancellationToken);
        if (!result.Success)
        {
            if (result.Error?.Contains("non trovata", StringComparison.OrdinalIgnoreCase) == true)
                return TypedResults.NotFound();
            if (result.Error?.Contains("stato NotTransmitted", StringComparison.OrdinalIgnoreCase) == true)
                return TypedResults.Conflict(result.Error);
            return TypedResults.BadRequest(result.Error ?? "Trasmissione nota di credito non riuscita.");
        }

        return TypedResults.Ok(result.Data!);
    }
}
