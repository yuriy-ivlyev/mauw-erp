using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mauw.Erp.Api.Endpoints;

public static class SupplierDocumentOcrEndpoints
{
    public static void MapSupplierDocumentOcrEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/v1/supplier-documents")
            .WithTags("Supplier documents")
            .RequireAuthorization(AppPolicies.WarehouseOperations)
            .MapPost("/parse", Parse)
            .DisableAntiforgery()
            .WithSummary("Estrae le righe articolo da un documento fornitore");
    }

    private static async Task<IResult> Parse(
        [FromForm] IFormFile? file,
        ISupplierDocumentOcrProvider provider,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return TypedResults.Problem(statusCode: 400, detail: "Caricare un documento non vuoto.");

        await using var stream = file.OpenReadStream();
        var result = await provider.ParseAsync(stream, cancellationToken);
        return result.Success
            ? TypedResults.Ok(result.Data)
            : TypedResults.Problem(statusCode: 400, detail: result.Error);
    }
}
