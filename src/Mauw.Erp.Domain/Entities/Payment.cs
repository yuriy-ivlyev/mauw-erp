namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Payment entity tracking invoice payments.
/// </summary>
public class Payment : Entity<Guid>
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? TransactionDetails { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;

    public Payment(
        Guid invoiceId,
        decimal amount,
        string paymentMethod,
        string? transactionDetails = null,
        DateTime? paymentDate = null
    ) : base()
    {
        InvoiceId = invoiceId;
        Amount = amount;
        PaymentDate = paymentDate ?? DateTime.UtcNow;
        PaymentMethod = paymentMethod;
        TransactionDetails = transactionDetails;
    }

    private Payment() { } // For EF Core
}