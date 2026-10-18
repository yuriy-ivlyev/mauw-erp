using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

public static class CassaSessionEndpoints
{
    public static void MapCassaSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/cassa/sessions")
            .WithName("CassaSessions");

        group.MapPost("/", CreateSession)
            .WithName("CreateCassaSession")
            .WithSummary("Crea e sospende una nuova sessione cassa");

        group.MapPost("/preview", PreviewCart)
            .WithName("PreviewCassaCart")
            .WithSummary("Calcola righe, castelletto IVA e totali del carrello senza salvare");

        group.MapGet("/paused", GetPausedSessions)
            .WithName("GetPausedCassaSessions")
            .WithSummary("Elenca le sessioni cassa sospese");

        group.MapGet("/{id:guid}", GetSession)
            .WithName("GetCassaSession")
            .WithSummary("Dettaglio sessione cassa con articoli");

        group.MapPatch("/{id:guid}/pause", PauseSession)
            .WithName("PauseCassaSession")
            .WithSummary("Aggiorna e sospende la sessione cassa");

        group.MapPatch("/{id:guid}/resume", ResumeSession)
            .WithName("ResumeCassaSession")
            .WithSummary("Riprende la sessione cassa su un terminale");

        group.MapPatch("/{id:guid}/terminate", TerminateSession)
            .WithName("TerminateCassaSession")
            .WithSummary("Termina definitivamente la sessione cassa");
    }

    private static async Task<Results<Ok<CassaSessionDetailDto>, BadRequest<string>>> CreateSession(
        CreateCassaSessionRequest request,
        IOrderService orderService)
    {
        var result = await orderService.CreateCassaSessionAsync(request);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.BadRequest(result.Error);
    }

    private static Results<Ok<CassaPreviewDto>, BadRequest<string>> PreviewCart(
        CassaPreviewRequest request,
        IOrderService orderService)
    {
        var result = orderService.PreviewCassaCart(request);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.BadRequest(result.Error);
    }

    private static async Task<Ok<IEnumerable<CassaSessionSummaryDto>>> GetPausedSessions(
        IOrderService orderService)
    {
        var result = await orderService.GetSuspendedSessionsAsync();
        return TypedResults.Ok(result.Data ?? []);
    }

    private static async Task<Results<Ok<CassaSessionDetailDto>, NotFound<string>>> GetSession(
        Guid id,
        IOrderService orderService)
    {
        var result = await orderService.GetCassaSessionAsync(id);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.NotFound(result.Error);
    }

    private static async Task<Results<Ok<CassaSessionDetailDto>, NotFound<string>>> PauseSession(
        Guid id,
        UpdateCassaSessionRequest request,
        IOrderService orderService)
    {
        var result = await orderService.UpdateCassaSessionAsync(id, request);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.NotFound(result.Error);
    }

    private static async Task<Results<Ok<CassaSessionDetailDto>, NotFound<string>>> ResumeSession(
        Guid id,
        ResumeCassaSessionRequest request,
        IOrderService orderService)
    {
        var result = await orderService.ResumeCassaSessionAsync(id, request.TerminalId);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.NotFound(result.Error);
    }

    private static async Task<Results<Ok, NotFound<string>>> TerminateSession(
        Guid id,
        IOrderService orderService)
    {
        var result = await orderService.TerminateCassaSessionAsync(id);
        return result.Success ? TypedResults.Ok() : TypedResults.NotFound(result.Error);
    }
}
