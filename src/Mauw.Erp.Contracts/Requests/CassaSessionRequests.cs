namespace Mauw.Erp.Contracts.Requests;

public record CreateCassaSessionRequest(
    string CartJson,
    string? ContactId,
    string? ContactName,
    string? DocumentNumber,
    string? TerminalId
);

public record PauseCassaSessionRequest(
    string CartJson,
    string? ContactId,
    string? ContactName,
    string? DocumentNumber,
    string? TerminalId
);

public record ResumeCassaSessionRequest(
    string? TerminalId
);
