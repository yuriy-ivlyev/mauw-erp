namespace Mauw.Erp.Contracts.Requests;

public record CassaCartItemRequest(
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    int MaxQuantity
);

public record CreateCassaSessionRequest(
    Guid ContactId,
    string? DocumentReference,
    string? TerminalId,
    List<CassaCartItemRequest> Items
);

public record UpdateCassaSessionRequest(
    Guid ContactId,
    string? DocumentReference,
    string? TerminalId,
    List<CassaCartItemRequest> Items
);

public record ResumeCassaSessionRequest(string? TerminalId);
