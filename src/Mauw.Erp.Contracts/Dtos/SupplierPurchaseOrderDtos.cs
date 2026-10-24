namespace Mauw.Erp.Contracts.Dtos;

/// <summary>A supplier order and its ordered and received variant quantities.</summary>
public sealed record SupplierPurchaseOrderDto(
    Guid Id, string OrderNumber, Guid SupplierId, string SupplierName, DateTimeOffset OrderDate,
    string Status, string? Notes, Guid Version, IReadOnlyList<SupplierPurchaseOrderLineDto> Lines);

/// <summary>A variant on a supplier order.</summary>
public sealed record SupplierPurchaseOrderLineDto(
    Guid Id, Guid ArticleVariantId, string Sku, string ArticleDescription, int QuantityOrdered, int QuantityReceived,
    decimal UnitCost);

/// <summary>Quantity comparison between a supplier order and confirmed receipts.</summary>
public sealed record SupplierReconciliationLineDto(
    Guid? SupplierPurchaseOrderLineId, Guid ArticleVariantId, string Sku, string ArticleDescription, int QuantityOrdered,
    int QuantityReceived, int Delta, string Status,
    IReadOnlyList<SupplierReconciliationReceiptDto> Receipts);

/// <summary>A confirmed DDT line contributing to a reconciliation row.</summary>
public sealed record SupplierReconciliationReceiptDto(
    Guid SupplierDeliveryNoteLineId, Guid SupplierDeliveryNoteId, string DocumentNumber,
    int Quantity, bool LinkedToOrderLine);

/// <summary>Reconciliation overview for a supplier order.</summary>
public sealed record SupplierReconciliationDto(
    Guid SupplierPurchaseOrderId, string OrderNumber, string Status,
    IReadOnlyList<SupplierReconciliationLineDto> Lines);

/// <summary>A generated reminder line for quantities still missing from the supplier.</summary>
public sealed record SupplierShortageLineDto(
    Guid ArticleVariantId, string Sku, string ArticleDescription, int QuantityMissing);

/// <summary>A draft supplier return document with the selected excess lines.</summary>
public sealed record SupplierReturnDraftDto(
    Guid Id, Guid SupplierPurchaseOrderId, Guid SupplierDeliveryNoteId, DateTimeOffset CreatedOn,
    DateTimeOffset? IssuedOn, string Status, Guid Version,
    IReadOnlyList<SupplierReturnDraftLineDto> Lines);

/// <summary>A selected received line and quantity prepared for supplier return.</summary>
public sealed record SupplierReturnDraftLineDto(
    Guid SupplierDeliveryNoteLineId, Guid ArticleVariantId, string Sku, string ArticleDescription, int Quantity);
