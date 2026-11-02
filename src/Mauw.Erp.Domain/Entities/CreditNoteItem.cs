namespace Mauw.Erp.Domain.Entities;

public class CreditNoteItem : Entity<Guid>, ICommercialDocumentLine
{
    public Guid CreditNoteId { get; private set; }
    public Guid? SourceInvoiceItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public bool IsShippingCharge { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitNetPrice { get; set; }
    public decimal LineNetTotal { get; set; }
    public decimal VatRate { get; private set; }
    public decimal? LineVatTotal { get; set; }
    public decimal? LineGrossTotal { get; set; }
    public string? NatureCode { get; set; }
    public decimal DiscountPercent { get; set; }

    public CreditNote CreditNote { get; private set; } = null!;
    public InvoiceItem? SourceInvoiceItem { get; private set; }
    public ProductVariant? Variant { get; set; }

    public CreditNoteItem(
        CreditNote creditNote,
        Guid? sourceInvoiceItemId,
        Guid? variantId,
        string description,
        bool isShippingCharge,
        decimal quantity,
        decimal unitNetPrice,
        decimal lineNetTotal,
        decimal vatRate,
        string? natureCode,
        decimal discountPercent,
        InvoiceItem? sourceInvoiceItem = null,
        ProductVariant? variant = null) : base()
    {
        CreditNoteId = creditNote.Id;
        CreditNote = creditNote;
        SourceInvoiceItemId = sourceInvoiceItemId;
        SourceInvoiceItem = sourceInvoiceItem;
        VariantId = variantId;
        Variant = variant;
        Description = description;
        IsShippingCharge = isShippingCharge;
        Quantity = quantity;
        UnitNetPrice = unitNetPrice;
        LineNetTotal = lineNetTotal;
        VatRate = vatRate;
        NatureCode = natureCode;
        DiscountPercent = discountPercent;
    }

    private CreditNoteItem() : base() { }
}
