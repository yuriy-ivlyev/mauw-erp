namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// DdtItem entity representing a line item in a DDT.
/// </summary>
public class DdtItem : Entity<Guid>, ICommercialDocumentLine
{
    public Guid DdtDocumentId { get; set; }
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
    public DdtDocument DdtDocument { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;

    public DdtItem(
        Guid ddtDocumentId,
        Guid variantId,
        int quantity,
        decimal unitNetPrice,
        decimal vatRate,
        string? natureCode = null,
        decimal discountPercent = 0m
    ) : base()
    {
        DdtDocumentId = ddtDocumentId;
        VariantId = variantId;
        Quantity = quantity;
        UnitNetPrice = unitNetPrice;
        VatRate = vatRate;
        NatureCode = natureCode;
        DiscountPercent = discountPercent;
    }

    private DdtItem() { } // For EF Core
}