namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for recording a payment.
/// </summary>
public class RecordPaymentRequest
{
    public decimal Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
}