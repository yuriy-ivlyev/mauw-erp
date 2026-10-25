using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Mauw.Erp.Api.Endpoints;

public static class SupplierDeliveryNoteEndpoints
{
    public static void MapSupplierDeliveryNoteEndpoints(this WebApplication app)
    {
        MapGroup(app, "/api/supplier-delivery-notes");
        MapGroup(app, "/api/v1/supplier-delivery-notes");
    }

    private static void MapGroup(WebApplication app, string path)
    {
        var group = app.MapGroup(path).WithTags("Supplier delivery notes")
            .RequireAuthorization(AppPolicies.WarehouseOperations);
        group.MapGet("/", List)
            .WithSummary("Elenca i DDT fornitore");
        group.MapGet("/{id:guid}", async Task<Results<Ok<SupplierDeliveryNoteDto>, NotFound>> (
            Guid id, ISupplierDeliveryNoteService service, CancellationToken ct) =>
        {
            var note = await service.GetAsync(id, ct);
            return note is null ? TypedResults.NotFound() : TypedResults.Ok(note);
        }).WithSummary("Legge testata, righe e versione del DDT");
        group.MapGet("/{id:guid}/article-match", async Task<Results<Ok<SupplierArticleMatchDto>, ProblemHttpResult>> (
            Guid id, string supplierItemCode, ISupplierDeliveryNoteService service, CancellationToken ct) =>
        {
            var result = await service.FindArticleAsync(id, supplierItemCode, ct);
            return result.Success ? TypedResults.Ok(result.Data!)
                : TypedResults.Problem(statusCode: result.Error == "Documento non trovato." ? 404
                    : result.Error!.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409 : 400,
                    detail: result.Error);
        }).WithSummary("Cerca un articolo per codice prodotto fornitore senza salvare il DDT")
            .WithDescription("Limita la ricerca al fornitore del DDT; risolve codici variante, mapping e codici prodotto univoci, poi il barcode della variante.");
        group.MapPost("/", Upload).DisableAntiforgery()
            .WithSummary("Carica un PDF o una scansione PNG/JPEG su S3 (massimo 20 MB)")
            .Produces<SupplierDeliveryNoteDto>(201);
        group.MapPut("/{id:guid}/lines", async (Guid id, SaveSupplierDeliveryNoteLinesRequest request,
            ISupplierDeliveryNoteService service, CancellationToken ct) =>
            Respond(await service.SaveLinesAsync(id, request, false, ct)))
            .WithSummary("Sostituisce le righe della bozza: aggiunta, modifica, mapping e rimozione");
        group.MapPost("/{id:guid}/parse", async (Guid id, SaveSupplierDeliveryNoteLinesRequest request,
            ISupplierDeliveryNoteService service, CancellationToken ct) =>
            Respond(await service.SaveLinesAsync(id, request, true, ct)))
            .WithSummary("Acquisisce un payload OCR/IA strutturato ed esegue il matching");
        group.MapPost("/{id:guid}/confirm", async (Guid id, SupplierDeliveryNoteActionRequest request,
            ISupplierDeliveryNoteService service, CancellationToken ct) =>
            Respond(await service.ConfirmAsync(id, request.Version, ct)))
            .WithSummary("Conferma atomicamente DDT, movimenti e giacenze intere");
        group.MapPost("/{id:guid}/cancel", async (Guid id, SupplierDeliveryNoteActionRequest request,
            ISupplierDeliveryNoteService service, CancellationToken ct) =>
            Respond(await service.CancelAsync(id, request.Version, ct)))
            .WithSummary("Annulla una bozza senza movimenti");
        group.MapGet("/{id:guid}/file", async Task<Results<FileStreamHttpResult, NotFound>> (
            Guid id, ISupplierDeliveryNoteService service, CancellationToken ct) =>
        {
            var file = await service.DownloadAsync(id, ct);
            return file is null ? TypedResults.NotFound()
                : TypedResults.File(file.Value.Stream, file.Value.ContentType, $"DDT-{id}{Extension(file.Value.ContentType)}");
        }).WithSummary("Scarica il documento originale senza esporre credenziali S3");
    }

    private static async Task<Results<Ok<PagedResult<SupplierDeliveryNoteListItemDto>>, BadRequest<string>>> List(
        ISupplierDeliveryNoteService service, CancellationToken ct, int page = 1, int pageSize = 20,
        string? search = null, Guid? supplierId = null, string? status = null,
        DateOnly? dateFrom = null, DateOnly? dateTo = null)
    {
        if (status is not null && !Enum.TryParse<Mauw.Erp.Domain.Enums.SupplierDeliveryNoteStatus>(status, true, out _))
            return TypedResults.BadRequest("Stato DDT fornitore non valido.");
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
            return TypedResults.BadRequest("L'intervallo date non e valido.");
        return TypedResults.Ok(await service.ListAsync(page, pageSize, search, supplierId, status, dateFrom, dateTo, ct));
    }

    private static async Task<IResult> Upload(
        [FromForm] Guid supplierId, [FromForm] Guid warehouseId,
        [FromForm] DateTimeOffset documentDate, [FromForm] string? documentNumber,
        [FromForm] Guid? supplierPurchaseOrderId,
        IFormFile file, ISupplierDeliveryNoteService service, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(new(supplierId, warehouseId, documentNumber, documentDate,
                supplierPurchaseOrderId),
            stream, file.FileName, file.ContentType, file.Length, ct);
        return result.Success
            ? TypedResults.Created($"/api/supplier-delivery-notes/{result.Data!.Id}", result.Data)
            : Respond(result);
    }

    private static IResult Respond(OperationResult<SupplierDeliveryNoteDto> result) =>
        result.Success ? TypedResults.Ok(result.Data)
            : TypedResults.Problem(statusCode: result.Error == "Documento non trovato." ? 404
                : result.Error!.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409 : 400,
                detail: result.Error);

    private static string Extension(string contentType) => contentType switch
    {
        "application/pdf" => ".pdf", "image/png" => ".png", _ => ".jpg"
    };
}
