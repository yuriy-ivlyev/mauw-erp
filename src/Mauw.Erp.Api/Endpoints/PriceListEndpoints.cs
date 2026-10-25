using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Costs;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Pricing;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

public static class PriceListEndpoints
{
    public static void MapPriceListEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/price-lists").WithTags("Listini")
            .RequireAuthorization(AppPolicies.AnyRole);

        group.MapGet("/", async (IPriceListService service, ICostVisibility costs, CancellationToken ct) =>
            TypedResults.Ok(costs.Redact(await service.GetAllAsync(ct))))
            .WithName("GetPriceLists").WithSummary("Elenca i listini e le regole (il valore delle regole CostMarkup solo ad Admin).");
        group.MapGet("/{id:guid}", async Task<Results<Ok<PriceListResponse>, ProblemHttpResult>> (
            Guid id, IPriceListService service, ICostVisibility costs, CancellationToken ct) =>
        {
            var list = await service.GetAsync(id, ct);
            return list is null ? Error("Listino non trovato.") : TypedResults.Ok(costs.Redact(list));
        }).WithName("GetPriceList").WithSummary("Legge un listino con le sue regole.");

        group.MapPost("/", async Task<Results<Created<PriceListResponse>, ProblemHttpResult>> (
            SavePriceListRequest request, IPriceListService service, ICostVisibility costs, CancellationToken ct) =>
        {
            var result = await service.SaveAsync(null, request, ct, !costs.CanSeeMargins);
            return result.Success
                ? TypedResults.Created($"/api/v1/price-lists/{result.Data!.Id}", costs.Redact(result.Data!))
                : Error(result.Error);
        }).RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("CreatePriceList").WithSummary("Crea un listino EUR.");

        group.MapPut("/{id:guid}", async Task<Results<Ok<PriceListResponse>, ProblemHttpResult>> (
            Guid id, SavePriceListRequest request, IPriceListService service, ICostVisibility costs, CancellationToken ct) =>
        {
            var result = await service.SaveAsync(id, request, ct, !costs.CanSeeMargins);
            return result.Success ? TypedResults.Ok(costs.Redact(result.Data!)) : Error(result.Error);
        }).RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("UpdatePriceList").WithSummary("Aggiorna il listino senza modificare gli snapshot documentali.");

        group.MapPatch("/{id:guid}/activation", async Task<Results<Ok<PriceListResponse>, ProblemHttpResult>> (
            Guid id, PriceListActivationRequest request, IPriceListService service, ICostVisibility costs, CancellationToken ct) =>
        {
            var result = await service.SetActivationAsync(id, request.IsActive, ct);
            return result.Success ? TypedResults.Ok(costs.Redact(result.Data!)) : Error(result.Error);
        }).RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("ActivatePriceList").WithSummary("Attiva o disattiva un listino.");

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, ProblemHttpResult>> (
            Guid id, IPriceListService service, CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(id, ct);
            return result.Success ? TypedResults.NoContent() : Error(result.Error);
        }).RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("DeletePriceList").WithSummary("Elimina il listino e scollega i clienti, mantenendo gli snapshot.");

        group.MapPost("/resolve", async Task<Results<Ok<ResolvedPricesResponse>, ProblemHttpResult>> (
            ResolvePricesRequest request, IPriceListService service, CancellationToken ct) =>
        {
            var result = await service.ResolveAsync(request, ct);
            return result.Success ? TypedResults.Ok(result.Data!) : Error(result.Error);
        }).WithName("ResolvePrices").WithSummary("Risolve in memoria un intero lotto di prezzi, senza query per riga.");
    }

    private static ProblemHttpResult Error(string? detail) => TypedResults.Problem(
        detail: detail, statusCode: detail == "Listino non trovato." ? 404 : 400,
        title: detail == "Listino non trovato." ? "Listino non trovato" : "Dati non validi");
}
