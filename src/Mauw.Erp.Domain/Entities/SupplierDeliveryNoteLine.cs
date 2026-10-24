using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class SupplierDeliveryNoteLine : Entity<Guid>
{
    public Guid SupplierDeliveryNoteId { get; set; }
    public SupplierDeliveryNote SupplierDeliveryNote { get; private set; } = null!;
    public int Position { get; set; }
    public string SupplierItemCode { get; set; } = "";
    public Guid? ArticleVariantId { get; set; }
    public ProductVariant? ArticleVariant { get; private set; }
    public Guid? SupplierPurchaseOrderLineId { get; set; }
    public SupplierPurchaseOrderLine? SupplierPurchaseOrderLine { get; private set; }
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public SupplierDeliveryNoteLineStatus LineStatus { get; set; }
    public StockMovement? StockMovement { get; private set; }

    public SupplierDeliveryNoteLine() : base(Guid.NewGuid()) { }
}
