namespace Mauw.Erp.Contracts.Dtos;

public record CassaSessionDto(
    Guid Id,
    string SessionCode,
    string Status,
    string CartJson,
    string? ContactId,
    string? ContactName,
    string? DocumentNumber,
    string? TerminalId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CassaSessionSummaryDto(
    Guid Id,
    string SessionCode,
    string? ContactName,
    int CartItemCount,
    DateTime UpdatedAt
);
