namespace Mauw.Erp.Contracts.Requests;

public record CassaCartItemRequest(
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    int MaxQuantity,
    string? NatureCode = null,
    decimal DiscountPercent = 0m,
    decimal? UnitGrossPrice = null
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
