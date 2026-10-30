using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// DDT (Transport Document) management endpoints.
/// Thin layer delegating to IDdtService.
/// </summary>
public static class DdtEndpoints
{
    public static void MapDdtEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ddts")
            .WithName("DDTs");

        group.MapPost("/", CreateDdt)
            .RequireAuthorization(AppPolicies.DdtDocuments)
            .WithName("CreateDdt")
            .WithSummary("Create a new transport document (DDT)");

        group.MapGet("/{id}", GetDdt)
            .RequireAuthorization(AppPolicies.AnyRole)
            .WithName("GetDdt")
            .WithSummary("Get DDT details by ID");

        group.MapGet("/", ListDdts)
            .RequireAuthorization(AppPolicies.WarehouseOperations)
            .WithName("ListDdts")
            .WithSummary("List DDTs with pagination");

        group.MapGet("/{id}/pdf", GetDdtPdf)
            .RequireAuthorization(AppPolicies.DdtDocuments)
            .WithName("GetDdtPdf")
            .WithSummary("Generate and download DDT PDF");

        group.MapPost("/{id}/items", AddDdtItem)
            .RequireAuthorization(AppPolicies.DdtDocuments)
            .WithName("AddDdtItem")
            .WithSummary("Add an item to a DDT");

        group.MapDelete("/{id}", DeleteDdt)
            .RequireAuthorization(AppPolicies.WarehouseOperations)
            .WithName("DeleteDdt")
            .WithSummary("Delete a DDT");
    }

    private static async Task<Results<Created<DdtDto>, BadRequest<string>>> CreateDdt(
        CreateDdtRequest request,
        IDdtService ddtService)
    {
        var result = await ddtService.CreateDdtAsync(request);

        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        return TypedResults.Created($"/api/v1/ddts/{result.Data!.Id}", result.Data);
    }

    private static async Task<Results<Ok<DdtDto>, NotFound>> GetDdt(
        Guid id,
        IDdtService ddtService)
    {
        var result = await ddtService.GetDdtAsync(id);

        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Ok<List<DdtDto>>> ListDdts(
        IDdtService ddtService,
        Guid? contactId = null,
        int page = 1,
        int pageSize = 20)
    {
        if (contactId.HasValue)
        {
            var result = await ddtService.GetDdtsByContactAsync(contactId.Value, page, pageSize);
            return TypedResults.Ok(result.Data!.Items);
        }

        return TypedResults.Ok(new List<DdtDto>());
    }

    private static async Task<Results<FileContentHttpResult, NotFound, BadRequest<string>>> GetDdtPdf(
        Guid id,
        IDdtService ddtService)
    {
        var result = await ddtService.GetDdtPdfAsync(id);
        if (!result.Success)
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? TypedResults.NotFound()
                : TypedResults.BadRequest(result.Error!);

        return TypedResults.File(result.Data!, "application/pdf", $"ddt-{id}.pdf");
    }

    private static async Task<Results<Ok<DdtDto>, BadRequest<string>, NotFound>> AddDdtItem(
        Guid id,
        CreateOrderItemRequest request,
        IDdtService ddtService)
    {
        var result = await ddtService.AddItemAsync(id, request);

        if (!result.Success)
        {
            if (result.Error != null && result.Error.Contains("not found"))
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteDdt(
        Guid id,
        IDdtService ddtService)
    {
        var result = await ddtService.DeleteDdtAsync(id);

        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }
}
