namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// InvoiceItem entity representing a line item in an invoice.
/// </summary>
public class InvoiceItem : Entity
{
    public Guid InvoiceId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal LineNetTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineVatTotal { get; set; }
    public decimal LineGrossTotal { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;

    public InvoiceItem(
        Guid invoiceId,
        Guid variantId,
        int quantity,
        decimal unitNetPrice,
        decimal vatRate
    ) : base()
    {
        InvoiceId = invoiceId;
        VariantId = variantId;
        Quantity = quantity;
        UnitNetPrice = unitNetPrice;
        VatRate = vatRate;

        // Calculate line totals
        LineNetTotal = decimal.Round(unitNetPrice * quantity, 2, MidpointRounding.AwayFromZero);
        LineVatTotal = decimal.Round(LineNetTotal * (vatRate / 100), 2, MidpointRounding.AwayFromZero);
        LineGrossTotal = LineNetTotal + LineVatTotal;
    }

    private InvoiceItem() { } // For EF Core
}