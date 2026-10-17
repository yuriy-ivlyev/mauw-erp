using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface ICassaSessionService
{
    Task<OperationResult<CassaSessionDto>> CreateSessionAsync(CreateCassaSessionRequest request);
    Task<OperationResult<CassaSessionDto>> PauseSessionAsync(Guid id, PauseCassaSessionRequest request);
    Task<OperationResult<CassaSessionDto>> ResumeSessionAsync(Guid id, ResumeCassaSessionRequest request);
    Task<OperationResult<bool>> TerminateSessionAsync(Guid id);
    Task<OperationResult<IEnumerable<CassaSessionSummaryDto>>> GetPausedSessionsAsync();
    Task<OperationResult<CassaSessionDto>> GetByIdAsync(Guid id);
}
