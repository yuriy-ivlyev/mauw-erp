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

        group.MapGet("/paused", GetPausedSessions)
            .WithName("GetPausedCassaSessions")
            .WithSummary("Elenca le sessioni cassa in pausa");

        group.MapGet("/{id:guid}", GetSession)
            .WithName("GetCassaSession")
            .WithSummary("Dettaglio sessione cassa");

        group.MapPatch("/{id:guid}/pause", PauseSession)
            .WithName("PauseCassaSession")
            .WithSummary("Sospende la sessione cassa");

        group.MapPatch("/{id:guid}/resume", ResumeSession)
            .WithName("ResumeCassaSession")
            .WithSummary("Riprende la sessione cassa su un terminale");

        group.MapPatch("/{id:guid}/terminate", TerminateSession)
            .WithName("TerminateCassaSession")
            .WithSummary("Termina definitivamente la sessione cassa");
    }

    private static async Task<Results<Ok<CassaSessionDto>, BadRequest<string>>> CreateSession(
        CreateCassaSessionRequest request,
        ICassaSessionService service)
    {
        var result = await service.CreateSessionAsync(request);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.BadRequest(result.Error);
    }

    private static async Task<Ok<IEnumerable<CassaSessionSummaryDto>>> GetPausedSessions(
        ICassaSessionService service)
    {
        var result = await service.GetPausedSessionsAsync();
        return TypedResults.Ok(result.Data ?? []);
    }

    private static async Task<Results<Ok<CassaSessionDto>, NotFound<string>>> GetSession(
        Guid id,
        ICassaSessionService service)
    {
        var result = await service.GetByIdAsync(id);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.NotFound(result.Error);
    }

    private static async Task<Results<Ok<CassaSessionDto>, NotFound<string>>> PauseSession(
        Guid id,
        PauseCassaSessionRequest request,
        ICassaSessionService service)
    {
        var result = await service.PauseSessionAsync(id, request);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.NotFound(result.Error);
    }

    private static async Task<Results<Ok<CassaSessionDto>, NotFound<string>>> ResumeSession(
        Guid id,
        ResumeCassaSessionRequest request,
        ICassaSessionService service)
    {
        var result = await service.ResumeSessionAsync(id, request);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.NotFound(result.Error);
    }

    private static async Task<Results<Ok, NotFound<string>>> TerminateSession(
        Guid id,
        ICassaSessionService service)
    {
        var result = await service.TerminateSessionAsync(id);
        return result.Success ? TypedResults.Ok() : TypedResults.NotFound(result.Error);
    }
}
