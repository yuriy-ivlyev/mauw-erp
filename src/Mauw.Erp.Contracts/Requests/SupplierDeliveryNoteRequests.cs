namespace Mauw.Erp.Contracts.Requests;

/// <summary>Metadata accompanying a supplier PDF or scanned image.</summary>
public sealed record UploadSupplierDeliveryNoteRequest(
    Guid SupplierId, Guid WarehouseId, string? DocumentNumber, DateTimeOffset DocumentDate,
    Guid? SupplierPurchaseOrderId = null);

/// <summary>A manually entered or OCR-extracted supplier line. Stock quantities must be positive integers.</summary>
public sealed record SupplierDeliveryNoteLineRequest
{
    public string SupplierItemCode { get; init; } = "";
    public Guid? ArticleVariantId { get; init; }
    public Guid? SupplierPurchaseOrderLineId { get; init; }
    public string Description { get; init; } = "";
    public int Quantity { get; init; }
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
    public decimal UnitPrice { get; init; }
}

/// <summary>Replaces draft lines atomically, including removals, and optionally updates extracted header fields.</summary>
public sealed record SaveSupplierDeliveryNoteLinesRequest
{
    public Guid Version { get; init; }
    public string DocumentNumber { get; init; } = "";
    public DateTimeOffset DocumentDate { get; init; }
    public IReadOnlyList<SupplierDeliveryNoteLineRequest> Lines { get; init; } = [];
}

/// <summary>Confirms or cancels the specified document revision.</summary>
public sealed record SupplierDeliveryNoteActionRequest(Guid Version);
