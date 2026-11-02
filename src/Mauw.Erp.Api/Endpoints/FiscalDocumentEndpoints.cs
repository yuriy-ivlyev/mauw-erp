using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Commands.FiscalDocuments;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

public static class FiscalDocumentEndpoints
{
    public static void MapFiscalDocumentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/fiscal-documents/bulk-transmit-sdi", TransmitBatch)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("BulkTransmitFiscalDocumentsToSdi")
            .WithSummary("Transmit invoices and credit notes to the SDI as an independent batch");
    }

    private static async Task<Results<Ok<BulkFiscalTransmissionResultDto>, BadRequest<string>>> TransmitBatch(
        BulkTransmitFiscalDocumentsRequest request,
        MediatR.ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new BulkTransmitFiscalDocumentsCommand(request.Documents), cancellationToken);
        return result.Success
            ? TypedResults.Ok(result.Data!)
            : TypedResults.BadRequest(result.Error ?? "Invio batch SDI non riuscito.");
    }
}
