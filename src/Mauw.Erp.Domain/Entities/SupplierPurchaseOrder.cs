using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class SupplierPurchaseOrder : Entity<Guid>
{
    public Guid SupplierId { get; set; }
    public Contact Supplier { get; private set; } = null!;
    public string OrderNumber { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public SupplierPurchaseOrderStatus Status { get; set; } = SupplierPurchaseOrderStatus.Draft;
    public string? Notes { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<SupplierPurchaseOrderLine> Lines { get; set; } = [];

    public SupplierPurchaseOrder() : base(Guid.NewGuid()) { }

    public void Confirm()
    {
        if (Status != SupplierPurchaseOrderStatus.Draft || Lines.Count == 0)
            throw new InvalidOperationException("Solo un ordine in bozza con almeno una riga puo essere confermato.");
        Status = SupplierPurchaseOrderStatus.Confirmed;
        UpdateTimestamp();
        Version = Guid.NewGuid();
    }

    public void RefreshProgress()
    {
        if (Status is SupplierPurchaseOrderStatus.ClosedWithDiscrepancies or SupplierPurchaseOrderStatus.Draft)
            return;
        Status = Lines.All(line => line.QuantityReceived == line.QuantityOrdered)
            ? SupplierPurchaseOrderStatus.Completed
            : Lines.Any(line => line.QuantityReceived > 0)
                ? SupplierPurchaseOrderStatus.PartiallyReceived
                : SupplierPurchaseOrderStatus.Confirmed;
        UpdateTimestamp();
        Version = Guid.NewGuid();
    }

    public void CloseWithDiscrepancies()
    {
        if (Status is SupplierPurchaseOrderStatus.Draft or SupplierPurchaseOrderStatus.ClosedWithDiscrepancies)
            throw new InvalidOperationException("L'ordine non puo essere chiuso nello stato corrente.");
        Status = SupplierPurchaseOrderStatus.ClosedWithDiscrepancies;
        Version = Guid.NewGuid();
        UpdateTimestamp();
    }
}
