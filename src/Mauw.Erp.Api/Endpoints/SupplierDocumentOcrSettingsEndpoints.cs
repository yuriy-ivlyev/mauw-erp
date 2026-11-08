using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Api.Endpoints;

public static class SupplierDocumentOcrSettingsEndpoints
{
    public static void MapSupplierDocumentOcrSettingsEndpoints(this WebApplication app)
    {
        var aliases = app.MapGroup("/api/v1/settings/supplier-document-ocr/aliases")
            .WithTags("OCR documenti fornitore")
            .RequireAuthorization(AppPolicies.AdminOnly);

        aliases.MapGet("/", async (
                ISupplierDocumentOcrAliasService service,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await service.ListAsync(cancellationToken)))
            .WithSummary("Elenca gli alias globali delle intestazioni OCR");

        aliases.MapPost("/", async (
                CreateSupplierDocumentOcrHeaderAliasRequest request,
                ISupplierDocumentOcrAliasService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.AddAsync(request, cancellationToken);
                return result.Success
                    ? Results.Created(
                        $"/api/v1/settings/supplier-document-ocr/aliases/{result.Data!.Id}",
                        result.Data)
                    : Results.Problem(statusCode: 400, detail: result.Error);
            })
            .WithSummary("Aggiunge un alias a un campo OCR");

        aliases.MapDelete("/{id:guid}", async (
                Guid id,
                ISupplierDocumentOcrAliasService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                if (result.Success) return Results.NoContent();
                return result.Error == "Alias OCR non trovato."
                    ? Results.NotFound()
                    : Results.Problem(statusCode: 400, detail: result.Error);
            })
            .WithSummary("Rimuove un alias globale delle intestazioni OCR");
    }
}
