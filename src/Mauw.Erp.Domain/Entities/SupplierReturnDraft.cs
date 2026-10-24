using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class SupplierReturnDraft : Entity<Guid>
{
    public Guid SupplierPurchaseOrderId { get; set; }
    public SupplierPurchaseOrder SupplierPurchaseOrder { get; private set; } = null!;
    public Guid SupplierDeliveryNoteId { get; set; }
    public SupplierDeliveryNote SupplierDeliveryNote { get; private set; } = null!;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public SupplierReturnDraftStatus Status { get; set; } = SupplierReturnDraftStatus.Draft;
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTime? IssuedOn { get; set; }
    public ICollection<SupplierReturnDraftLine> Lines { get; set; } = [];

    public SupplierReturnDraft() : base(Guid.NewGuid()) { }
}
