namespace Mauw.Erp.Contracts.Requests;

/// <summary>Creates a draft order for a supplier and its article variants.</summary>
public sealed record CreateSupplierPurchaseOrderRequest
{
    public Guid SupplierId { get; init; }
    public DateTimeOffset OrderDate { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<CreateSupplierPurchaseOrderLineRequest> Lines { get; init; } = [];
}

/// <summary>An ordered variant, quantity and agreed net unit cost.</summary>
public sealed record CreateSupplierPurchaseOrderLineRequest
{
    public Guid ArticleVariantId { get; init; }
    public int QuantityOrdered { get; init; }
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
    public decimal UnitCost { get; init; }
}

/// <summary>Confirms or force-closes an order using the current revision.</summary>
public sealed record SupplierPurchaseOrderActionRequest(Guid Version);

/// <summary>Creates a supplier return draft for selected received delivery-note lines.</summary>
public sealed record CreateSupplierReturnDraftRequest(IReadOnlyList<SupplierReturnDraftLineRequest> Lines);

/// <summary>Issues a return draft using its current revision.</summary>
public sealed record SupplierReturnDraftActionRequest(Guid Version);

/// <summary>A received DDT line selected for return, with a positive quantity.</summary>
public sealed record SupplierReturnDraftLineRequest(Guid SupplierDeliveryNoteLineId, int Quantity);
