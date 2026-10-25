using Mauw.Erp.Application.Constants;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

public static class CompanyLogoEndpoints
{
    public static void MapCompanyLogoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/settings/company/logos");
        group.MapGet("/{name:regex(^(horizontal|compact)$)}", GetLogo)
            .AllowAnonymous()
            .WithName("GetCompanyLogo")
            .WithSummary("Logo aziendale PNG per UI e stampe, senza risorse esterne");
        group.MapPut("/{name:regex(^(horizontal|compact)$)}", UpdateLogo)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireRole(AppRoles.Admin))
            .WithName("UpdateCompanyLogo")
            .WithSummary("Sostituisce un logo aziendale nel database (solo amministratori)");
    }

    private static async Task<Results<FileContentHttpResult, NotFound<string>>> GetLogo(
        string name, ICompanyLogoService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.GetAsync(name, ct);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return result.Success
            ? TypedResults.File(result.Data!.PngData, "image/png")
            : TypedResults.NotFound(result.Error);
    }

    private static async Task<Results<NoContent, BadRequest<string>>> UpdateLogo(
        string name, UpdateCompanyLogoRequest request, ICompanyLogoService service, CancellationToken ct)
    {
        var result = await service.UpdateAsync(name, request, ct);
        return result.Success ? TypedResults.NoContent() : TypedResults.BadRequest(result.Error);
    }
}
