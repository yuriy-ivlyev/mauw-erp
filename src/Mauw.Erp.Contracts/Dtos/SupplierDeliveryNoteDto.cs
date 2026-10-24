namespace Mauw.Erp.Contracts.Dtos;

/// <summary>An article resolved by supplier item code without saving the delivery note.</summary>
public sealed record SupplierArticleMatchDto(Guid ArticleVariantId, string ArticleDescription);

/// <summary>Supplier delivery note, including its current concurrency revision and editable lines.</summary>
public sealed record SupplierDeliveryNoteDto(
    Guid Id, Guid SupplierId, string SupplierName, Guid WarehouseId, string WarehouseName,
    string DocumentNumber, DateTimeOffset DocumentDate, DateTimeOffset UploadedAt,
    Guid? SupplierPurchaseOrderId,
    string FilePath, string Status, Guid Version, IReadOnlyList<SupplierDeliveryNoteLineDto> Lines);

/// <summary>A paged supplier delivery note row with line and piece totals.</summary>
public sealed record SupplierDeliveryNoteListItemDto(
    Guid Id, string DocumentNumber, DateTimeOffset DocumentDate, string SupplierName,
    string WarehouseName, int LineCount, int PieceCount, string Status);

/// <summary>A matched or unresolved supplier line with movement traceability.</summary>
public sealed record SupplierDeliveryNoteLineDto(
    Guid Id, string SupplierItemCode, Guid? ArticleVariantId, string? ArticleDescription, string Description,
    Guid? SupplierPurchaseOrderLineId,
    int Quantity,
    [property: System.Text.Json.Serialization.JsonNumberHandling(
        System.Text.Json.Serialization.JsonNumberHandling.WriteAsString | System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
    decimal UnitPrice, string LineStatus, Guid? StockMovementId);
