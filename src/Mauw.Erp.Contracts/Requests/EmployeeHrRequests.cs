namespace Mauw.Erp.Contracts.Requests;

public sealed record SaveEmployeeDocumentRequest(
    string Kind,
    string Title,
    string? DocumentNumber,
    string? IssuerOrProvider,
    DateOnly? IssuedOn,
    DateOnly? CompletedOn,
    DateOnly? ExpiresOn,
    string? Notes,
    Guid? Version = null);

public sealed record SaveEmployeeTimeEntryRequest(
    DateOnly WorkDate,
    string Type,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int BreakMinutes,
    string? Notes,
    Guid? Version = null);
