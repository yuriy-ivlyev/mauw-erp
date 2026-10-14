namespace Mauw.Erp.Domain.Entities;

using Enums;

/// <summary>
/// Invoice entity representing a customer invoice.
/// Can be immediate or deferred from DDTs.
/// </summary>
public class Invoice : Entity
{
    public Guid ContactId { get; set; }
    public string InvoiceNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal PaidAmount { get; set; } = 0;

    // Navigation
    public Contact Contact { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<AgentCommission> Commissions { get; set; } = [];

    public Invoice(
        Guid contactId,
        string invoiceNumber
    ) : base()
    {
        ContactId = contactId;
        InvoiceNumber = invoiceNumber;
        InvoiceDate = DateTime.UtcNow;
        TaxableTotal = 0;
        VatTotal = 0;
        GrossTotal = 0;
    }

    /// <summary>
    /// Adds an item to the invoice and updates totals.
    /// </summary>
    public void AddItem(InvoiceItem item)
    {
        Items.Add(item);
        UpdateTotals();
    }

    /// <summary>
    /// Recalculates invoice totals from items.
    /// </summary>
    public void UpdateTotals()
    {
        TaxableTotal = Items.Sum(i => i.LineNetTotal);
        VatTotal = Items.Sum(i => i.LineVatTotal);
        GrossTotal = TaxableTotal + VatTotal;
        UpdateTimestamp();
    }

    /// <summary>
    /// Records a payment and checks if commissions should be accrued.
    /// </summary>
    public void RecordPayment(decimal amount)
    {
        PaidAmount += amount;
        UpdateTimestamp();

        // If fully paid, trigger commission accrual
        if (PaidAmount >= GrossTotal)
        {
            foreach (var commission in Commissions.Where(c => c.Status == CommissionStatus.Pending))
            {
                commission.Accrue();
            }
        }
    }
}