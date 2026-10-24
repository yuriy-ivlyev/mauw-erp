namespace Mauw.Erp.Domain.Entities;

public sealed class SupplierPurchaseOrderLine : Entity<Guid>
{
    public Guid SupplierPurchaseOrderId { get; set; }
    public SupplierPurchaseOrder SupplierPurchaseOrder { get; private set; } = null!;
    public Guid ArticleVariantId { get; set; }
    public ProductVariant ArticleVariant { get; private set; } = null!;
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }

    public SupplierPurchaseOrderLine() : base(Guid.NewGuid()) { }
}
