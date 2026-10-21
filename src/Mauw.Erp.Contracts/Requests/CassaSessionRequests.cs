namespace Mauw.Erp.Contracts.Requests;

public record CassaCartItemRequest(
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    int MaxQuantity,
    string? NatureCode = null,
    decimal DiscountPercent = 0m,
    Guid? PriceListId = null,
    bool IsManualPriceOverride = false,
    string? OverrideReason = null,
    bool IsCustomerReturn = false,
    Guid? SourceLineId = null,
    Mauw.Erp.Contracts.Pricing.CashDiscountRequest? CashDiscount = null,
    string? PriceListDiscountReason = null
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

public record CassaPreviewRequest(List<CassaCartItemRequest> Items);
