using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Mauw.Erp.Application.Services;

public class CassaSessionService(
    ICassaSessionRepository repository,
    ILogger<CassaSessionService> logger) : ICassaSessionService
{
    public async Task<OperationResult<CassaSessionDto>> CreateSessionAsync(CreateCassaSessionRequest request)
    {
        var count = await repository.CountAllAsync();
        var code = $"CASSA-{(count + 1):D3}";

        var session = new CassaSession(
            code,
            request.CartJson,
            request.ContactId,
            request.ContactName,
            request.DocumentNumber,
            request.TerminalId);

        await repository.AddAsync(session);
        await repository.SaveAsync();

        logger.LogInformation("Created cassa session {Code}", code);
        return OperationResult<CassaSessionDto>.Ok(Map(session));
    }

    public async Task<OperationResult<CassaSessionDto>> PauseSessionAsync(Guid id, PauseCassaSessionRequest request)
    {
        var session = await repository.GetByIdAsync(id);
        if (session is null)
            return OperationResult<CassaSessionDto>.Fail("Sessione non trovata");

        session.Pause(request.CartJson, request.ContactId, request.ContactName, request.DocumentNumber, request.TerminalId);
        await repository.SaveAsync();

        logger.LogInformation("Paused cassa session {Id}", id);
        return OperationResult<CassaSessionDto>.Ok(Map(session));
    }

    public async Task<OperationResult<CassaSessionDto>> ResumeSessionAsync(Guid id, ResumeCassaSessionRequest request)
    {
        var session = await repository.GetByIdAsync(id);
        if (session is null)
            return OperationResult<CassaSessionDto>.Fail("Sessione non trovata");

        session.Resume(request.TerminalId);
        await repository.SaveAsync();

        logger.LogInformation("Resumed cassa session {Id}", id);
        return OperationResult<CassaSessionDto>.Ok(Map(session));
    }

    public async Task<OperationResult<bool>> TerminateSessionAsync(Guid id)
    {
        var session = await repository.GetByIdAsync(id);
        if (session is null)
            return OperationResult<bool>.Fail("Sessione non trovata");

        session.Terminate();
        await repository.SaveAsync();

        logger.LogInformation("Terminated cassa session {Id}", id);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<IEnumerable<CassaSessionSummaryDto>>> GetPausedSessionsAsync()
    {
        var sessions = await repository.GetPausedSessionsAsync();
        var dtos = sessions.Select(s =>
        {
            var count = 0;
            try
            {
                var items = JsonSerializer.Deserialize<JsonElement[]>(s.CartJson);
                count = items?.Length ?? 0;
            }
            catch { }

            return new CassaSessionSummaryDto(s.Id, s.SessionCode, s.ContactName, count, s.UpdatedAt);
        });

        return OperationResult<IEnumerable<CassaSessionSummaryDto>>.Ok(dtos);
    }

    public async Task<OperationResult<CassaSessionDto>> GetByIdAsync(Guid id)
    {
        var session = await repository.GetByIdAsync(id);
        if (session is null)
            return OperationResult<CassaSessionDto>.Fail("Sessione non trovata");

        return OperationResult<CassaSessionDto>.Ok(Map(session));
    }

    private static CassaSessionDto Map(CassaSession s) =>
        new(s.Id, s.SessionCode, s.Status.ToString(), s.CartJson,
            s.ContactId, s.ContactName, s.DocumentNumber, s.TerminalId,
            s.CreatedAt, s.UpdatedAt);
}
