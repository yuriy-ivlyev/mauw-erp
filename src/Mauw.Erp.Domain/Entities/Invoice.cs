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

    private Invoice() { } // For EF Core

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

/// <summary>
/// Payment entity tracking invoice payments.
/// </summary>
public class Payment : Entity
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;

    public Payment(
        Guid invoiceId,
        decimal amount,
        DateTime? paymentDate = null
    ) : base()
    {
        InvoiceId = invoiceId;
        Amount = amount;
        PaymentDate = paymentDate ?? DateTime.UtcNow;
    }

    private Payment() { } // For EF Core
}

/// <summary>
/// AgentCommission entity tracking agent commissions.
/// Commissions are calculated on Net Taxable Amount (Imponibile Netto merce),
/// excluding VAT and transport fees.
/// Status transitions: Pending -> Accrued -> Paid
/// </summary>
public class AgentCommission : Entity
{
    public Guid AgentId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal TaxableAmountBase { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public CommissionStatus Status { get; set; }

    // Navigation
    public Contact Agent { get; set; } = null!;
    public Invoice Invoice { get; set; } = null!;

    public AgentCommission(
        Guid agentId,
        Guid invoiceId,
        decimal taxableAmountBase,
        decimal commissionRate
    ) : base()
    {
        AgentId = agentId;
        InvoiceId = invoiceId;
        TaxableAmountBase = taxableAmountBase;
        CommissionRate = commissionRate;
        CommissionAmount = decimal.Round(taxableAmountBase * (commissionRate / 100), 2, MidpointRounding.AwayFromZero);
        Status = CommissionStatus.Pending;
    }

    private AgentCommission() { } // For EF Core

    /// <summary>
    /// Transitions commission status from Pending to Accrued.
    /// Called when invoice is fully paid.
    /// </summary>
    public void Accrue()
    {
        if (Status != CommissionStatus.Pending)
            throw new InvalidOperationException("Only pending commissions can be accrued.");

        Status = CommissionStatus.Accrued;
        UpdateTimestamp();
    }

    /// <summary>
    /// Transitions commission status from Accrued to Paid.
    /// </summary>
    public void MarkAsPaid()
    {
        if (Status != CommissionStatus.Accrued)
            throw new InvalidOperationException("Only accrued commissions can be marked as paid.");

        Status = CommissionStatus.Paid;
        UpdateTimestamp();
    }
}
