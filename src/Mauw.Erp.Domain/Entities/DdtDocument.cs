namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// DdtDocument entity representing a transport document (Documento di Trasporto).
/// Created from confirmed orders and decrements physical stock.
/// </summary>
public class DdtDocument : Entity<Guid>
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