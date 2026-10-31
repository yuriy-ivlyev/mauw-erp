namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Draft order preview: the server applies the tax regime of the customer before computing the fiscal summary.
/// </summary>
public record OrderPreviewRequest(
    Guid ContactId,
    List<CassaCartItemRequest> Items,
    decimal ShippingCost = 0.00m,
    decimal? ShippingVatRate = null,
    string? ShippingNatureCode = null);
