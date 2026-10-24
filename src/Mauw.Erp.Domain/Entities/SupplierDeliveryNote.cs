using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class SupplierDeliveryNote : Entity<Guid>
{
    public Guid SupplierId { get; set; }
    public Contact Supplier { get; private set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public Guid? SupplierPurchaseOrderId { get; set; }
    public SupplierPurchaseOrder? SupplierPurchaseOrder { get; private set; }
    public string DocumentNumber { get; set; } = "";
    public DateTime DocumentDate { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string FilePath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public SupplierDeliveryNoteStatus Status { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<SupplierDeliveryNoteLine> Lines { get; set; } = [];

    public SupplierDeliveryNote() : base(Guid.NewGuid()) { }
}
