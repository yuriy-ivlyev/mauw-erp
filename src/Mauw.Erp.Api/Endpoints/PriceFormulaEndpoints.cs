using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Pricing;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>Formula prezzi globale: configurazione (solo Admin) e calcolo del prezzo senza salvataggio.</summary>
public static class PriceFormulaEndpoints
{
    public static void MapPriceFormulaEndpoints(this WebApplication app)
    {
        var settings = app.MapGroup("/api/v1/settings/price-formula").WithTags("Formula prezzi")
            .RequireAuthorization(AppPolicies.AdminOnly);

        settings.MapGet("/", async (IPriceFormulaService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetAsync(ct)))
            .WithName("GetPriceFormula")
            .WithSummary("Legge la formula prezzi; se non configurata restituisce isConfigured=false.");

        settings.MapPut("/", async Task<Results<Ok<PriceFormulaDto>, ProblemHttpResult>> (
                SavePriceFormulaRequest request, IPriceFormulaService service,
                ICurrentUserService currentUser, CancellationToken ct) =>
            {
                var result = await service.SaveAsync(request, currentUser.UserId, ct);
                return result.Success ? TypedResults.Ok(result.Data!) : Error(result.Error);
            })
            .WithName("SavePriceFormula")
            .WithSummary("Salva la formula prezzi globale sostituendo i passaggi esistenti.");

        // Gruppo separato: la rotta letterale non puo essere intercettata dalle rotte /{id} dei prodotti
        // e la sola policy Admin esclude Magazzino, Vendite e Contabilita.
        var calculation = app.MapGroup("/api/v1/products/price-formula").WithTags("Formula prezzi")
            .RequireAuthorization(AppPolicies.AdminOnly);

        calculation.MapPost("/calculate", async Task<Results<Ok<PriceFormulaCalculationDto>, ProblemHttpResult>> (
                CalculatePriceRequest request, IPriceFormulaService service, CancellationToken ct) =>
            {
                var result = await service.CalculateAsync(request.PurchaseCost, ct);
                return result.Success ? TypedResults.Ok(result.Data!) : Error(result.Error);
            })
            .WithName("CalculatePriceFormula")
            .WithSummary("Calcola il prezzo finale dal costo d'acquisto con la formula salvata, senza salvare nulla.");
    }

    private static ProblemHttpResult Error(string? detail) =>
        TypedResults.Problem(detail: detail, statusCode: 400, title: "Dati non validi");
}
