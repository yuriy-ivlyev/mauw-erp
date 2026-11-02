namespace Mauw.Erp.Domain.Entities;

using Mauw.Erp.Domain.Enums;

public class CreditNote : Entity<Guid>
{
    public Guid InvoiceId { get; set; }
    public string CreditNoteNumber { get; set; }
    public DateTime CreditNoteDate { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal AllocatedAmount { get; private set; } = 0.00m;
    public decimal RemainingCredit => Math.Max(0m, GrossTotal - AllocatedAmount);
    public string? Reason { get; set; }
    public string? RelatedInvoiceNumber { get; private set; }
    public DateTime? RelatedInvoiceDate { get; private set; }
    public bool ReturnToWarehouse { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public SdiStatus SdiStatus { get; private set; } = SdiStatus.NotTransmitted;
    public string? SdiFileName { get; private set; }
    public string? SdiStorageKey { get; private set; }
    public string? SdiTransmissionId { get; private set; }
    public DateTime? SdiTransmittedAtUtc { get; private set; }

    public Invoice Invoice { get; set; } = null!;
    public Warehouse? Warehouse { get; private set; }
    public ICollection<CreditNoteItem> Items { get; set; } = [];

    public CreditNote(Guid invoiceId, string creditNoteNumber) : base(Guid.NewGuid())
    {
        InvoiceId = invoiceId;
        CreditNoteNumber = creditNoteNumber;
        CreditNoteDate = DateTime.UtcNow;
    }

    public CreditNote(
        Invoice invoice,
        string creditNoteNumber,
        string reason,
        bool returnToWarehouse,
        Guid? warehouseId,
        DateTime issuedAtUtc) : this(invoice.Id, creditNoteNumber)
    {
        Invoice = invoice;
        Reason = reason;
        RelatedInvoiceNumber = invoice.InvoiceNumber;
        RelatedInvoiceDate = invoice.InvoiceDate;
        ReturnToWarehouse = returnToWarehouse;
        WarehouseId = warehouseId;
        CreditNoteDate = issuedAtUtc;
    }

    public CreditNoteWorkflowResult MarkAsTransmitted(
        string fileName,
        string storageKey,
        string? transmissionId,
        DateTime transmittedAtUtc)
    {
        if (SdiStatus != SdiStatus.NotTransmitted)
            return CreditNoteWorkflowResult.Fail("La nota di credito non è in stato NotTransmitted.");
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 50)
            return CreditNoteWorkflowResult.Fail("Il nome file SDI è obbligatorio e non può superare 50 caratteri.");
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > 255)
            return CreditNoteWorkflowResult.Fail("La chiave di archiviazione SDI è obbligatoria e non può superare 255 caratteri.");
        if (transmissionId?.Length > 100)
            return CreditNoteWorkflowResult.Fail("L'identificativo di trasmissione SDI non può superare 100 caratteri.");
        if (transmittedAtUtc.Kind != DateTimeKind.Utc)
            return CreditNoteWorkflowResult.Fail("La data di trasmissione deve essere espressa in UTC.");

        SdiFileName = fileName;
        SdiStorageKey = storageKey;
        SdiTransmissionId = string.IsNullOrWhiteSpace(transmissionId) ? null : transmissionId;
        SdiTransmittedAtUtc = transmittedAtUtc;
        SdiStatus = SdiStatus.Transmitted;
        UpdateTimestamp();
        return CreditNoteWorkflowResult.Ok();
    }

    public CreditNoteWorkflowResult MarkSdiAccepted()
    {
        if (SdiStatus != SdiStatus.Transmitted)
            return CreditNoteWorkflowResult.Fail("Solo una nota di credito trasmessa può essere accettata dallo SDI.");

        SdiStatus = SdiStatus.Accepted;
        UpdateTimestamp();
        return CreditNoteWorkflowResult.Ok();
    }

    public CreditNoteWorkflowResult AllocateCredit(decimal amount)
    {
        if (amount <= 0m || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
            return CreditNoteWorkflowResult.Fail("Importo compensazione non valido.");
        if (amount > RemainingCredit)
            return CreditNoteWorkflowResult.Fail($"Credito residuo insufficiente ({RemainingCredit:C2}).");

        AllocatedAmount += amount;
        UpdateTimestamp();
        return CreditNoteWorkflowResult.Ok();
    }
}
