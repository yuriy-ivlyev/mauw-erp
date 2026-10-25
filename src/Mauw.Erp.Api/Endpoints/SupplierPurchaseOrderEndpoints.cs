using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

public static class SupplierPurchaseOrderEndpoints
{
    public static void MapSupplierPurchaseOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/supplier-purchase-orders")
            .WithTags("Supplier purchase orders")
            .RequireAuthorization(AppPolicies.WarehouseOperations);

        group.MapGet("/", async Task<Ok<IReadOnlyList<SupplierPurchaseOrderDto>>> (
            ISupplierPurchaseOrderService service, CancellationToken ct, Guid? supplierId = null,
            bool openOnly = false) =>
            TypedResults.Ok(await service.ListAsync(supplierId, openOnly, ct)))
            .WithSummary("Elenca gli ordini fornitore")
            .WithDescription("Consente di filtrare per fornitore e limitare agli ordini ancora aperti.");

        group.MapGet("/{id:guid}", async Task<Results<Ok<SupplierPurchaseOrderDto>, NotFound>> (
            Guid id, ISupplierPurchaseOrderService service, CancellationToken ct) =>
        {
            var order = await service.GetAsync(id, ct);
            return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
        }).WithSummary("Legge un ordine fornitore");

        group.MapPost("/", async Task<IResult> (
            CreateSupplierPurchaseOrderRequest request, ISupplierPurchaseOrderService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.Success
                ? TypedResults.Created($"/api/supplier-purchase-orders/{result.Data!.Id}", result.Data)
                : TypedResults.Problem(statusCode: 400, detail: result.Error);
        }).WithSummary("Crea una bozza di ordine fornitore")
            .Produces<SupplierPurchaseOrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/confirm", async Task<IResult> (
            Guid id, SupplierPurchaseOrderActionRequest request, ISupplierPurchaseOrderService service,
            CancellationToken ct) =>
        {
            var result = await service.ConfirmAsync(id, request.Version, ct);
            return result.Success ? TypedResults.Ok(result.Data)
                : TypedResults.Problem(statusCode: result.Error!.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409 : 400,
                    detail: result.Error);
        }).WithSummary("Conferma l'ordine fornitore");

        group.MapGet("/{id:guid}/reconciliation", async Task<Results<Ok<SupplierReconciliationDto>, NotFound>> (
            Guid id, ISupplierPurchaseOrderService service, CancellationToken ct) =>
        {
            var result = await service.ReconcileAsync(id, ct);
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }).WithSummary("Confronta quantità ordinate e ricevute per variante");

        group.MapGet("/{id:guid}/shortages", async Task<Results<Ok<IReadOnlyList<SupplierShortageLineDto>>, NotFound>> (
            Guid id, ISupplierPurchaseOrderService service, CancellationToken ct) =>
        {
            var result = await service.ShortagesAsync(id, ct);
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }).WithSummary("Genera la lista delle quantità mancanti");

        group.MapPost("/{id:guid}/return-drafts", async Task<IResult> (
            Guid id, CreateSupplierReturnDraftRequest request, ISupplierPurchaseOrderService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateReturnDraftAsync(id, request, ct);
            return result.Success
                ? TypedResults.Created($"/api/supplier-purchase-orders/{id}/return-drafts/{result.Data!.Id}", result.Data)
                : TypedResults.Problem(statusCode: result.Error == "Ordine fornitore non trovato." ? 404 : 400,
                    detail: result.Error);
        }).WithSummary("Prepara una bozza di DDT di reso per le eccedenze")
            .WithDescription("La bozza non modifica la giacenza: lo scarico avverrà con la successiva emissione del reso.")
            .Produces<SupplierReturnDraftDto>(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}/return-drafts", async Task<Results<Ok<IReadOnlyList<SupplierReturnDraftDto>>, NotFound>> (
            Guid id, ISupplierPurchaseOrderService service, CancellationToken ct) =>
        {
            var drafts = await service.ReturnDraftsAsync(id, ct);
            return drafts is null ? TypedResults.NotFound() : TypedResults.Ok(drafts);
        }).WithSummary("Elenca le bozze di reso dell'ordine");

        group.MapGet("/{id:guid}/return-drafts/{draftId:guid}",
            async Task<Results<Ok<SupplierReturnDraftDto>, NotFound>> (
                Guid id, Guid draftId, ISupplierPurchaseOrderService service, CancellationToken ct) =>
            {
                var draft = await service.GetReturnDraftAsync(id, draftId, ct);
                return draft is null ? TypedResults.NotFound() : TypedResults.Ok(draft);
            }).WithSummary("Legge una bozza di reso");

        group.MapPost("/{id:guid}/return-drafts/{draftId:guid}/issue", async Task<IResult> (
            Guid id, Guid draftId, SupplierReturnDraftActionRequest request,
            ISupplierPurchaseOrderService service, CancellationToken ct) =>
        {
            var result = await service.IssueReturnDraftAsync(id, draftId, request.Version, ct);
            return result.Success ? TypedResults.Ok(result.Data)
                : TypedResults.Problem(statusCode: result.Error == "Bozza di reso non trovata."
                    || result.Error == "Ordine fornitore non trovato." ? 404
                    : result.Error!.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409 : 400,
                    detail: result.Error);
        }).WithSummary("Emette il reso e scarica atomicamente le giacenze");

        group.MapPost("/{id:guid}/close", async Task<IResult> (
            Guid id, SupplierPurchaseOrderActionRequest request, ISupplierPurchaseOrderService service,
            CancellationToken ct) =>
        {
            var result = await service.CloseAsync(id, request.Version, ct);
            return result.Success ? TypedResults.Ok(result.Data)
                : TypedResults.Problem(statusCode: result.Error == "Ordine fornitore non trovato." ? 404
                    : result.Error!.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409 : 400,
                    detail: result.Error);
        }).WithSummary("Chiude l'ordine accettando le discrepanze");
    }
}
