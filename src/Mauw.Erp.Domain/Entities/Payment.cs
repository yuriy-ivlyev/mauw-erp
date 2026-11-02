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
    public Guid? SourceCreditNoteId { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;
    public CreditNote? SourceCreditNote { get; set; }

    public Payment(
        Guid invoiceId,
        decimal amount,
        string paymentMethod,
        string? transactionDetails = null,
        DateTime? paymentDate = null,
        Guid? sourceCreditNoteId = null,
        CreditNote? sourceCreditNote = null
    ) : base()
    {
        InvoiceId = invoiceId;
        Amount = amount;
        PaymentDate = paymentDate ?? DateTime.UtcNow;
        PaymentMethod = paymentMethod;
        TransactionDetails = transactionDetails;
        SourceCreditNoteId = sourceCreditNoteId;
        SourceCreditNote = sourceCreditNote;
    }

    private Payment() { } // For EF Core
}