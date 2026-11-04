using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Mauw.Erp.Api.Endpoints;

public static class PayableDocumentEndpoints
{
    public static void MapPayableDocumentEndpoints(this WebApplication app)
    {
        var management = app.MapGroup("/api/v1/payables")
            .WithTags("Payables")
            .RequireAuthorization(AppPolicies.AccountsPayable);

        management.MapGet("/", List)
            .WithSummary("Elenca fatture passive e documenti spesa");
        management.MapPost("/", CreateSupplierInvoice)
            .WithSummary("Registra una fattura fornitore in bozza");
        management.MapGet("/{id:guid}", GetManaged)
            .WithSummary("Legge un documento passivo");
        management.MapPut("/{id:guid}", UpdateManaged)
            .WithSummary("Aggiorna una bozza di documento passivo");
        management.MapPost("/{id:guid}/attachment", UploadManaged).DisableAntiforgery()
            .WithSummary("Carica l'allegato originale di una bozza");
        management.MapGet("/{id:guid}/attachment", DownloadManaged)
            .WithSummary("Scarica l'allegato originale");
        management.MapPost("/{id:guid}/submit", SubmitManaged)
            .WithSummary("Invia un documento in approvazione");
        management.MapPost("/{id:guid}/approve", Approve)
            .WithSummary("Approva una fattura passiva o un rimborso");
        management.MapPost("/{id:guid}/reject", Reject)
            .WithSummary("Respinge un documento in attesa di approvazione");
        management.MapPost("/{id:guid}/cancel", CancelManaged)
            .WithSummary("Annulla una bozza o una richiesta non approvata");
        management.MapPost("/{id:guid}/payments", RecordPayment)
            .WithSummary("Registra un pagamento parziale o totale");

        var ownReimbursements = app.MapGroup("/api/v1/payables/my-reimbursements")
            .WithTags("Payables")
            .RequireAuthorization(AppPolicies.AnyRole);
        ownReimbursements.MapGet("/", ListMyReimbursements)
            .WithSummary("Elenca i rimborsi dell'utente autenticato");
        ownReimbursements.MapPost("/", CreateReimbursement)
            .WithSummary("Registra una richiesta di rimborso in bozza");
        ownReimbursements.MapGet("/{id:guid}", GetOwnReimbursement)
            .WithSummary("Legge un rimborso dell'utente autenticato");
        ownReimbursements.MapPut("/{id:guid}", UpdateOwnReimbursement)
            .WithSummary("Aggiorna una bozza di rimborso");
        ownReimbursements.MapPost("/{id:guid}/attachment", UploadOwnReimbursement).DisableAntiforgery()
            .WithSummary("Carica il giustificativo di un rimborso");
        ownReimbursements.MapGet("/{id:guid}/attachment", DownloadOwnReimbursement)
            .WithSummary("Scarica il giustificativo del rimborso");
        ownReimbursements.MapPost("/{id:guid}/submit", SubmitOwnReimbursement)
            .WithSummary("Invia un rimborso in approvazione");
        ownReimbursements.MapPost("/{id:guid}/cancel", CancelOwnReimbursement)
            .WithSummary("Annulla una bozza di rimborso");
    }

    private static async Task<IResult> List(
        IPayableDocumentService service,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? kind = null,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(kind)
            && (!Enum.TryParse<PayableDocumentKind>(kind, true, out var parsedKind)
                || !Enum.IsDefined(parsedKind)))
            return TypedResults.BadRequest("Tipo documento non valido.");
        if (!string.IsNullOrWhiteSpace(status)
            && (!Enum.TryParse<PayableDocumentStatus>(status, true, out var parsedStatus)
                || !Enum.IsDefined(parsedStatus)))
            return TypedResults.BadRequest("Stato documento non valido.");
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
            return TypedResults.BadRequest("L'intervallo date non è valido.");

        return TypedResults.Ok(await service.ListAsync(
            page, pageSize, search, kind, status, dateFrom, dateTo, cancellationToken));
    }

    private static async Task<IResult> ListMyReimbursements(
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
            return TypedResults.Unauthorized();
        return TypedResults.Ok(await service.ListMyReimbursementsAsync(
            currentUser.UserId, page, pageSize, cancellationToken));
    }

    private static async Task<IResult> CreateSupplierInvoice(
        CreatePayableDocumentRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.CreateSupplierInvoiceAsync(
            request, currentUser.UserId, cancellationToken), created: true);

    private static async Task<IResult> CreateReimbursement(
        CreatePayableDocumentRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
            return TypedResults.Unauthorized();
        return ToResult(await service.CreateReimbursementAsync(
            request, currentUser.UserId, cancellationToken), created: true);
    }

    private static async Task<IResult> GetManaged(
        Guid id,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        await Get(id, requestingUserId: null, canManageAll: true, service, cancellationToken);

    private static async Task<IResult> GetOwnReimbursement(
        Guid id,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        await Get(id, currentUser.UserId, canManageAll: false, service, cancellationToken);

    private static async Task<IResult> Get(
        Guid id,
        string? requestingUserId,
        bool canManageAll,
        IPayableDocumentService service,
        CancellationToken cancellationToken)
    {
        var document = await service.GetAsync(id, requestingUserId, canManageAll, cancellationToken);
        return document is null ? TypedResults.NotFound() : TypedResults.Ok(document);
    }

    private static async Task<IResult> UpdateManaged(
        Guid id,
        CreatePayableDocumentRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.UpdateDraftAsync(
            id, request, currentUser.UserId, canManageAll: true, cancellationToken));

    private static async Task<IResult> UpdateOwnReimbursement(
        Guid id,
        CreatePayableDocumentRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.UpdateDraftAsync(
            id, request, currentUser.UserId, canManageAll: false, cancellationToken));

    private static async Task<IResult> UploadManaged(
        Guid id,
        Guid version,
        IFormFile file,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        await Upload(id, version, file, currentUser.UserId, canManageAll: true, service, cancellationToken);

    private static async Task<IResult> UploadOwnReimbursement(
        Guid id,
        Guid version,
        IFormFile file,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        await Upload(id, version, file, currentUser.UserId, canManageAll: false, service, cancellationToken);

    private static async Task<IResult> Upload(
        Guid id,
        Guid version,
        IFormFile file,
        string? requestingUserId,
        bool canManageAll,
        IPayableDocumentService service,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return TypedResults.BadRequest("Allegato obbligatorio.");
        await using var stream = file.OpenReadStream();
        var result = await service.UploadAttachmentAsync(
            id, version, stream, file.FileName, file.ContentType, file.Length,
            requestingUserId, canManageAll, cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> DownloadManaged(
        Guid id,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        await Download(id, requestingUserId: null, canManageAll: true, service, cancellationToken);

    private static async Task<IResult> DownloadOwnReimbursement(
        Guid id,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        await Download(id, currentUser.UserId, canManageAll: false, service, cancellationToken);

    private static async Task<IResult> Download(
        Guid id,
        string? requestingUserId,
        bool canManageAll,
        IPayableDocumentService service,
        CancellationToken cancellationToken)
    {
        var file = await service.DownloadAttachmentAsync(
            id, requestingUserId, canManageAll, cancellationToken);
        return file is null
            ? TypedResults.NotFound()
            : TypedResults.File(file.Value.Stream, file.Value.ContentType, file.Value.FileName);
    }

    private static async Task<IResult> SubmitManaged(
        Guid id,
        PayableDocumentActionRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.SubmitAsync(
            id, request.Version, currentUser.UserId, canManageAll: true, cancellationToken));

    private static async Task<IResult> SubmitOwnReimbursement(
        Guid id,
        PayableDocumentActionRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.SubmitAsync(
            id, request.Version, currentUser.UserId, canManageAll: false, cancellationToken));

    private static async Task<IResult> Approve(
        Guid id,
        PayableDocumentActionRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.ApproveAsync(id, request.Version, currentUser.UserId, cancellationToken));

    private static async Task<IResult> Reject(
        Guid id,
        RejectPayableDocumentRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.RejectAsync(
            id, request.Version, currentUser.UserId, request.Reason, cancellationToken));

    private static async Task<IResult> CancelManaged(
        Guid id,
        PayableDocumentActionRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.CancelAsync(
            id, request.Version, currentUser.UserId, canManageAll: true, cancellationToken));

    private static async Task<IResult> CancelOwnReimbursement(
        Guid id,
        PayableDocumentActionRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.CancelAsync(
            id, request.Version, currentUser.UserId, canManageAll: false, cancellationToken));

    private static async Task<IResult> RecordPayment(
        Guid id,
        PayableDocumentPaymentRequest request,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.RecordPaymentAsync(id, request, currentUser.UserId, cancellationToken));

    private static IResult ToResult(OperationResult<PayableDocumentDto> result, bool created = false)
    {
        if (result.Success)
            return created
                ? TypedResults.Created($"/api/v1/payables/{result.Data!.Id}", result.Data)
                : TypedResults.Ok(result.Data);
        var error = result.Error ?? "Operazione sul documento passivo non riuscita.";
        var status = error == "Documento passivo non trovato." ? 404
            : error.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409
            : error.Contains("già registrata", StringComparison.OrdinalIgnoreCase) ? 409
            : 400;
        return TypedResults.Problem(statusCode: status, detail: error);
    }
}
