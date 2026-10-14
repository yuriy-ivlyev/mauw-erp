namespace Mauw.Erp.Domain.Entities;

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