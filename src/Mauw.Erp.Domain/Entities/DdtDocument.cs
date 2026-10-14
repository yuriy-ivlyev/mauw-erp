namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// DdtDocument entity representing a transport document (Documento di Trasporto).
/// Created from confirmed orders and decrements physical stock.
/// </summary>
public class DdtDocument : Entity
{
    public Guid ContactId { get; set; }
    public string DdtNumber { get; set; }
    public DateTime DdtDate { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrossTotal { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;
    public ICollection<DdtItem> Items { get; set; } = [];

    public DdtDocument(
        Guid contactId,
        string ddtNumber
    ) : base()
    {
        ContactId = contactId;
        DdtNumber = ddtNumber;
        DdtDate = DateTime.UtcNow;
        TaxableTotal = 0;
        VatTotal = 0;
        GrossTotal = 0;
    }

    private DdtDocument() { } // For EF Core

    /// <summary>
    /// Adds an item to the DDT and updates totals.
    /// </summary>
    public void AddItem(DdtItem item)
    {
        Items.Add(item);
        UpdateTotals();
    }

    /// <summary>
    /// Recalculates DDT totals from items.
    /// </summary>
    public void UpdateTotals()
    {
        TaxableTotal = Items.Sum(i => i.LineNetTotal);
        VatTotal = Items.Sum(i => i.LineVatTotal);
        GrossTotal = TaxableTotal + VatTotal;
        UpdateTimestamp();
    }
}

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
