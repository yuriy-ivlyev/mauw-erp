namespace Mauw.Erp.Contracts.Dtos;

public sealed record CustomerStatementDto(
    Guid CustomerId,
    string CustomerName,
    decimal TotalInvoiced,
    decimal TotalCredited,
    decimal TotalPaid,
    decimal NetBalance,
    decimal AvailableCreditNotesAmount,
    List<StatementMovementDto> Movements);

public sealed record StatementMovementDto(
    DateTime DateUtc,
    string Type,
    Guid DocumentId,
    string DocumentNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string? PaymentMethod,
    string? SourceCreditNoteNumber,
    string? SdiStatus,
    Guid? SourceCreditNoteId = null);
