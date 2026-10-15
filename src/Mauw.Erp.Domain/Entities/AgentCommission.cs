using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// AgentCommission entity tracking agent commissions.
/// Commissions are calculated on Net Taxable Amount (Imponibile Netto merce),
/// excluding VAT and transport fees.
/// Status transitions: Pending -> Accrued -> Paid
/// </summary>
public class AgentCommission : Entity<Guid>
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