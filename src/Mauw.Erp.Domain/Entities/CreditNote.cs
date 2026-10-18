namespace Mauw.Erp.Domain.Entities;

public class CreditNote : Entity<Guid>
{
    public Guid InvoiceId { get; set; }
    public string CreditNoteNumber { get; set; }
    public DateTime CreditNoteDate { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrossTotal { get; set; }
    public string? Reason { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public CreditNote(Guid invoiceId, string creditNoteNumber)
    {
        InvoiceId = invoiceId;
        CreditNoteNumber = creditNoteNumber;
        CreditNoteDate = DateTime.UtcNow;
    }
}
