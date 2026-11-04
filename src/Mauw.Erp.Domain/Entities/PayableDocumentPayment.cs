namespace Mauw.Erp.Domain.Entities;

public sealed class PayableDocumentPayment : Entity<Guid>
{
    public Guid PayableDocumentId { get; private set; }
    public PayableDocument PayableDocument { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateTime PaymentDateUtc { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public string? TransactionDetails { get; private set; }
    public string? CreatedByUserId { get; private set; }

    public PayableDocumentPayment(
        Guid payableDocumentId,
        decimal amount,
        DateTime paymentDate,
        string paymentMethod,
        string? transactionDetails,
        string? createdByUserId) : base()
    {
        PayableDocumentId = payableDocumentId;
        Amount = amount;
        PaymentDateUtc = ToUtc(paymentDate);
        PaymentMethod = paymentMethod.Trim();
        TransactionDetails = Normalize(transactionDetails);
        CreatedByUserId = Normalize(createdByUserId);
    }

    private PayableDocumentPayment() { }

    private static DateTime ToUtc(DateTime dateTime) =>
        dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
