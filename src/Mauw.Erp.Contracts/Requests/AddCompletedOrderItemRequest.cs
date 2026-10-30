namespace Mauw.Erp.Contracts.Requests;

public sealed record AddCompletedOrderItemRequest(
    Guid VariantId,
    int Quantity,
    string? NatureCode);
