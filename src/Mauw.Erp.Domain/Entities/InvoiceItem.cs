namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// InvoiceItem entity representing a line item in an invoice.
/// </summary>
public class InvoiceItem : Entity<Guid>, ICommercialDocumentLine
{
    public Guid InvoiceId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal LineNetTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal? LineVatTotal { get; set; }
    public decimal? LineGrossTotal { get; set; }
    public string? NatureCode { get; set; }
    public decimal DiscountPercent { get; set; }
    public Mauw.Erp.Domain.Pricing.PricingSnapshot? PricingSnapshot { get; init; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;

    public InvoiceItem(
        Guid invoiceId,
        Guid variantId,
        int quantity,
        decimal unitNetPrice,
        decimal vatRate,
        string? natureCode = null,
        decimal discountPercent = 0m
    ) : base()
    {
        InvoiceId = invoiceId;
        VariantId = variantId;
        Quantity = quantity;
        UnitNetPrice = unitNetPrice;
        VatRate = vatRate;
        NatureCode = natureCode;
        DiscountPercent = discountPercent;
    }

    private InvoiceItem() { } // For EF Core
}