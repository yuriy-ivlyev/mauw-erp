namespace Mauw.Erp.Domain.Entities;

public sealed class SupplierReturnDraftLine : Entity<Guid>
{
    public Guid SupplierReturnDraftId { get; set; }
    public SupplierReturnDraft SupplierReturnDraft { get; private set; } = null!;
    public Guid SupplierDeliveryNoteLineId { get; set; }
    public SupplierDeliveryNoteLine SupplierDeliveryNoteLine { get; private set; } = null!;
    public Guid ArticleVariantId { get; set; }
    public ProductVariant ArticleVariant { get; private set; } = null!;
    public int Quantity { get; set; }

    public SupplierReturnDraftLine() : base(Guid.NewGuid()) { }
}
