namespace Mauw.Erp.Contracts.Dtos;

public sealed record BulkTransmissionResultDto(
    int RequestedCount,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<BulkTransmissionItemResultDto> Results);

public sealed record BulkTransmissionItemResultDto(
    Guid InvoiceId,
    bool Success,
    string? InvoiceNumber,
    string? ErrorMessage);
