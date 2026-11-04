namespace Mauw.Erp.Contracts.Dtos;

public sealed record EmployeeDocumentDto(
    Guid Id,
    Guid EmployeeId,
    string Kind,
    string Title,
    string? DocumentNumber,
    string? IssuerOrProvider,
    DateOnly? IssuedOn,
    DateOnly? CompletedOn,
    DateOnly? ExpiresOn,
    string? Notes,
    string FileName,
    string ContentType,
    long FileLength,
    DateTime CreatedAtUtc,
    Guid Version);

public sealed record EmployeeTimeEntryDto(
    Guid Id,
    Guid EmployeeId,
    DateOnly WorkDate,
    string Type,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int BreakMinutes,
    int WorkedMinutes,
    string? Notes,
    Guid Version);
