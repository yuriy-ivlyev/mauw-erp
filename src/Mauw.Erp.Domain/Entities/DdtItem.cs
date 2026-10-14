namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// DdtItem entity representing a line item in a DDT.
/// </summary>
public class DdtItem : Entity
{
    public Guid DdtDocumentId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal LineNetTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineVatTotal { get; set; }
    public decimal LineGrossTotal { get; set; }

    // Navigation
    public DdtDocument DdtDocument { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;

    public DdtItem(
        Guid ddtDocumentId,
        Guid variantId,
        int quantity,
        decimal unitNetPrice,
        decimal vatRate
    ) : base()
    {
        DdtDocumentId = ddtDocumentId;
        VariantId = variantId;
        Quantity = quantity;
        UnitNetPrice = unitNetPrice;
        VatRate = vatRate;

        // Calculate line totals
        LineNetTotal = decimal.Round(unitNetPrice * quantity, 2, MidpointRounding.AwayFromZero);
        LineVatTotal = decimal.Round(LineNetTotal * (vatRate / 100), 2, MidpointRounding.AwayFromZero);
        LineGrossTotal = LineNetTotal + LineVatTotal;
    }

    private DdtItem() { } // For EF Core
}