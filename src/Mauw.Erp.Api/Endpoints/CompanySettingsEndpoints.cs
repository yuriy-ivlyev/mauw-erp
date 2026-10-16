using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Gestione Nostra Azienda: dati societari, sede legale, sedi operative.
/// </summary>
public static class CompanySettingsEndpoints
{
    public static void MapCompanySettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/settings/company")
            .WithName("CompanySettings");

        group.MapGet("/", GetCompany)
            .WithName("GetCompanySettings")
            .WithSummary("Restituisce i dati della Nostra Azienda");

        group.MapPut("/", UpsertCompany)
            .WithName("UpsertCompanySettings")
            .WithSummary("Crea o aggiorna la Nostra Azienda");

        group.MapPost("/operational-sites", AddOperationalSite)
            .WithName("AddCompanyOperationalSite")
            .WithSummary("Aggiunge una sede operativa aziendale");

        group.MapPut("/operational-sites/{siteId:guid}", UpdateOperationalSite)
            .WithName("UpdateCompanyOperationalSite")
            .WithSummary("Aggiorna una sede operativa aziendale");

        group.MapDelete("/operational-sites/{siteId:guid}", DeleteOperationalSite)
            .WithName("DeleteCompanyOperationalSite")
            .WithSummary("Elimina una sede operativa aziendale");
    }

    private static async Task<Results<Ok<CompanySettingsDto>, NotFound<string>>> GetCompany(
        ICompanySettingsService service)
    {
        var result = await service.GetCompanyAsync();
        return result.Success
            ? TypedResults.Ok(result.Data!)
            : TypedResults.NotFound(result.Error);
    }

    private static async Task<Results<Ok<CompanySettingsDto>, BadRequest<string>>> UpsertCompany(
        UpdateCompanySettingsRequest request,
        ICompanySettingsService service)
    {
        var result = await service.UpsertCompanyAsync(request);
        return result.Success
            ? TypedResults.Ok(result.Data!)
            : TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<Created<CompanyAddressSiteDto>, BadRequest<string>>> AddOperationalSite(
        CompanyAddressSiteRequest request,
        ICompanySettingsService service)
    {
        var result = await service.AddOperationalSiteAsync(request);
        if (!result.Success) return TypedResults.BadRequest(result.Error);
        return TypedResults.Created($"/api/v1/settings/company/operational-sites/{result.Data!.Id}", result.Data);
    }

    private static async Task<Results<Ok<CompanyAddressSiteDto>, BadRequest<string>, NotFound>> UpdateOperationalSite(
        Guid siteId,
        CompanyAddressSiteRequest request,
        ICompanySettingsService service)
    {
        var result = await service.UpdateOperationalSiteAsync(siteId, request);
        if (!result.Success)
        {
            if (result.Error == "Sede non trovata.") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> DeleteOperationalSite(
        Guid siteId,
        ICompanySettingsService service)
    {
        var result = await service.DeleteOperationalSiteAsync(siteId);
        if (!result.Success)
        {
            if (result.Error == "Sede non trovata." || result.Error == "Azienda non configurata.")
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }
        return TypedResults.NoContent();
    }
}
