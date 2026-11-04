using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class PayableDocument : Entity<Guid>
{
    public PayableDocumentKind Kind { get; private set; }
    public string DocumentCode { get; private set; } = string.Empty;
    public Guid? SupplierId { get; private set; }
    public Contact? Supplier { get; private set; }
    public string? SourceDocumentNumber { get; private set; }
    public DateOnly DocumentDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string? BeneficiaryName { get; private set; }
    public string? CreatedByUserId { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? Notes { get; private set; }
    public PayableDocumentStatus Status { get; private set; }
    public decimal TaxableTotal { get; private set; } = 0.00m;
    public decimal VatTotal { get; private set; } = 0.00m;
    public decimal GrossTotal { get; private set; } = 0.00m;
    public decimal PaidAmount { get; private set; } = 0.00m;
    public decimal BalanceDue => Status == PayableDocumentStatus.Approved
        ? GrossTotal - PaidAmount
        : 0.00m;
    public PayablePaymentStatus PaymentStatus => Status != PayableDocumentStatus.Approved
        ? PayablePaymentStatus.NotPayable
        : PaidAmount == 0m
            ? PayablePaymentStatus.Unpaid
            : BalanceDue == 0m ? PayablePaymentStatus.Paid : PayablePaymentStatus.PartiallyPaid;
    public string? AttachmentKey { get; private set; }
    public string? AttachmentFileName { get; private set; }
    public string? AttachmentContentType { get; private set; }
    public long? AttachmentLength { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public ICollection<PayableDocumentLine> Lines { get; private set; } = [];
    public ICollection<PayableDocumentPayment> Payments { get; private set; } = [];

    public PayableDocument(
        PayableDocumentKind kind,
        string? createdByUserId) : base(Guid.NewGuid())
    {
        Kind = kind;
        DocumentCode = $"AP-{Id.ToString("N")[..16].ToUpperInvariant()}";
        CreatedByUserId = Normalize(createdByUserId);
        Status = PayableDocumentStatus.Draft;
    }

    private PayableDocument() { }

    public PayableDocumentOperationResult UpdateDraft(
        Guid? supplierId,
        string? sourceDocumentNumber,
        DateOnly documentDate,
        DateOnly dueDate,
        string? beneficiaryName,
        string? notes,
        IReadOnlyList<PayableDocumentLine>? lines)
    {
        if (Status is not (PayableDocumentStatus.Draft or PayableDocumentStatus.Rejected))
            return PayableDocumentOperationResult.Fail("Solo una bozza o un documento respinto può essere modificato.");
        if (documentDate == default || dueDate == default || dueDate < documentDate)
            return PayableDocumentOperationResult.Fail("Indicare date valide e una scadenza non precedente al documento.");
        if (sourceDocumentNumber?.Length > 100)
            return PayableDocumentOperationResult.Fail("Il riferimento del documento non può superare 100 caratteri.");
        if (lines is null || lines.Count is 0 or > 200 || lines.Any(line => line is null))
            return PayableDocumentOperationResult.Fail("Il documento deve contenere da 1 a 200 righe.");
        if (notes?.Length > 2000)
            return PayableDocumentOperationResult.Fail("Le note non possono superare 2000 caratteri.");

        if (Kind == PayableDocumentKind.SupplierInvoice)
        {
            if (!supplierId.HasValue || supplierId == Guid.Empty)
                return PayableDocumentOperationResult.Fail("Selezionare il fornitore.");
            if (string.IsNullOrWhiteSpace(sourceDocumentNumber) || sourceDocumentNumber.Length > 100)
                return PayableDocumentOperationResult.Fail("Il numero della fattura fornitore è obbligatorio (massimo 100 caratteri).");
            if (!string.IsNullOrWhiteSpace(beneficiaryName))
                return PayableDocumentOperationResult.Fail("Il beneficiario è previsto solo per un rimborso.");
        }
        else if (Kind == PayableDocumentKind.ExpenseReimbursement)
        {
            if (supplierId.HasValue)
                return PayableDocumentOperationResult.Fail("Un rimborso non può avere un fornitore.");
            if (string.IsNullOrWhiteSpace(beneficiaryName) || beneficiaryName.Length > 200)
                return PayableDocumentOperationResult.Fail("Indicare un beneficiario (massimo 200 caratteri).");
        }
        else
        {
            return PayableDocumentOperationResult.Fail("Tipo documento non supportato.");
        }

        foreach (var line in lines)
        {
            var validation = ValidateLine(line);
            if (!validation.Success)
                return validation;
        }

        Lines.Clear();
        foreach (var line in lines)
            Lines.Add(line);

        SupplierId = supplierId;
        SourceDocumentNumber = Normalize(sourceDocumentNumber);
        DocumentDate = documentDate;
        DueDate = dueDate;
        BeneficiaryName = Normalize(beneficiaryName);
        Notes = Normalize(notes);
        TaxableTotal = Money(Lines.Sum(line => line.TaxableAmount));
        VatTotal = Money(Lines.Sum(line => line.VatAmount));
        GrossTotal = Money(Lines.Sum(line => line.GrossAmount));
        if (Status == PayableDocumentStatus.Rejected)
        {
            Status = PayableDocumentStatus.Draft;
            SubmittedAtUtc = null;
            RejectionReason = null;
        }
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return PayableDocumentOperationResult.Ok();
    }

    public PayableDocumentOperationResult SetAttachment(
        string storageKey,
        string fileName,
        string contentType,
        long length)
    {
        if (Status != PayableDocumentStatus.Draft)
            return PayableDocumentOperationResult.Fail("L'allegato può essere aggiornato solo su una bozza.");
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > 500
            || string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255
            || string.IsNullOrWhiteSpace(contentType) || contentType.Length > 100
            || length <= 0 || length > 20 * 1024 * 1024)
            return PayableDocumentOperationResult.Fail("Allegato non valido o superiore a 20 MB.");

        AttachmentKey = storageKey;
        AttachmentFileName = fileName.Trim();
        AttachmentContentType = contentType;
        AttachmentLength = length;
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return PayableDocumentOperationResult.Ok();
    }

    public PayableDocumentOperationResult SubmitForApproval(DateTime submittedAtUtc)
    {
        if (Status != PayableDocumentStatus.Draft)
            return PayableDocumentOperationResult.Fail("Solo una bozza può essere inviata in approvazione.");
        if (Lines.Count == 0 || GrossTotal <= 0m)
            return PayableDocumentOperationResult.Fail("Il documento deve contenere importi positivi.");
        if (string.IsNullOrWhiteSpace(AttachmentKey))
            return PayableDocumentOperationResult.Fail("Allegare il documento originale prima dell'invio.");
        if (submittedAtUtc.Kind != DateTimeKind.Utc)
            return PayableDocumentOperationResult.Fail("La data di invio deve essere UTC.");

        Status = PayableDocumentStatus.PendingApproval;
        SubmittedAtUtc = submittedAtUtc;
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return PayableDocumentOperationResult.Ok();
    }

    public PayableDocumentOperationResult Approve(string? approvedByUserId, DateTime approvedAtUtc)
    {
        if (Status != PayableDocumentStatus.PendingApproval)
            return PayableDocumentOperationResult.Fail("Solo un documento in attesa può essere approvato.");
        if (string.IsNullOrWhiteSpace(approvedByUserId) || approvedAtUtc.Kind != DateTimeKind.Utc)
            return PayableDocumentOperationResult.Fail("Approvatore e data UTC sono obbligatori.");

        Status = PayableDocumentStatus.Approved;
        ApprovedByUserId = approvedByUserId.Trim();
        ApprovedAtUtc = approvedAtUtc;
        RejectionReason = null;
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return PayableDocumentOperationResult.Ok();
    }

    public PayableDocumentOperationResult Reject(string? reason)
    {
        if (Status != PayableDocumentStatus.PendingApproval)
            return PayableDocumentOperationResult.Fail("Solo un documento in attesa può essere respinto.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return PayableDocumentOperationResult.Fail("Il motivo del rifiuto è obbligatorio (massimo 500 caratteri).");

        Status = PayableDocumentStatus.Rejected;
        RejectionReason = reason.Trim();
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return PayableDocumentOperationResult.Ok();
    }

    public PayableDocumentOperationResult Cancel()
    {
        if (Status is not (PayableDocumentStatus.Draft or PayableDocumentStatus.PendingApproval
            or PayableDocumentStatus.Rejected))
            return PayableDocumentOperationResult.Fail("Un documento approvato non può essere annullato.");

        Status = PayableDocumentStatus.Cancelled;
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return PayableDocumentOperationResult.Ok();
    }

    public PayableDocumentOperationResult RecordPayment(
        decimal amount,
        DateTime paymentDate,
        string? paymentMethod,
        string? transactionDetails,
        string? createdByUserId)
    {
        if (Status != PayableDocumentStatus.Approved)
            return PayableDocumentOperationResult.Fail("I pagamenti sono ammessi solo per documenti approvati.");
        if (amount <= 0m || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
            return PayableDocumentOperationResult.Fail("L'importo del pagamento deve essere positivo e avere al massimo due decimali.");
        if (amount > BalanceDue)
            return PayableDocumentOperationResult.Fail("Il pagamento supera il residuo da saldare.");
        if (string.IsNullOrWhiteSpace(paymentMethod) || paymentMethod.Length > 50)
            return PayableDocumentOperationResult.Fail("Il metodo di pagamento è obbligatorio (massimo 50 caratteri).");
        if (transactionDetails?.Length > 500)
            return PayableDocumentOperationResult.Fail("Il riferimento del pagamento non può superare 500 caratteri.");
        if (paymentDate == default)
            return PayableDocumentOperationResult.Fail("La data del pagamento è obbligatoria.");

        Payments.Add(new PayableDocumentPayment(
            Id, amount, paymentDate, paymentMethod, transactionDetails, createdByUserId));
        PaidAmount = Money(PaidAmount + amount);
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return PayableDocumentOperationResult.Ok();
    }

    private static PayableDocumentOperationResult ValidateLine(PayableDocumentLine line)
    {
        if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Length > 500)
            return PayableDocumentOperationResult.Fail("La descrizione di ogni riga è obbligatoria (massimo 500 caratteri).");
        if (!Enum.IsDefined(line.Category))
            return PayableDocumentOperationResult.Fail("Categoria di spesa non valida.");
        if (line.CostCenter?.Length > 100 || line.Project?.Length > 100)
            return PayableDocumentOperationResult.Fail("Centro di costo e progetto non possono superare 100 caratteri.");
        if (!HasMoneyScale(line.TaxableAmount) || !HasMoneyScale(line.VatAmount)
            || !HasMoneyScale(line.GrossAmount)
            || line.TaxableAmount < 0m || line.VatAmount < 0m || line.GrossAmount <= 0m
            || line.GrossAmount != line.TaxableAmount + line.VatAmount)
            return PayableDocumentOperationResult.Fail("Gli importi devono essere positivi, a due decimali e coerenti tra imponibile, IVA e totale.");
        if (!line.VatRate.HasValue)
            return line.VatAmount == 0m && line.NatureCode is null
                ? PayableDocumentOperationResult.Ok()
                : PayableDocumentOperationResult.Fail("Aliquota mancante per la riga con IVA o Natura.");
        if (line.VatRate is < 0m or > 100m
            || decimal.Round(line.VatRate.Value, 2, MidpointRounding.AwayFromZero) != line.VatRate.Value)
            return PayableDocumentOperationResult.Fail("L'aliquota IVA deve essere tra 0 e 100 e avere al massimo due decimali.");
        if (line.VatRate == 0m && line.VatAmount != 0m)
            return PayableDocumentOperationResult.Fail("Una riga ad aliquota zero non può riportare IVA.");
        if (line.VatRate == 0m && string.IsNullOrWhiteSpace(line.NatureCode))
            return PayableDocumentOperationResult.Fail("La Natura IVA è obbligatoria con aliquota zero.");
        if (line.VatRate > 0m && line.NatureCode is not null)
            return PayableDocumentOperationResult.Fail("La Natura IVA deve essere assente con aliquota positiva.");

        return PayableDocumentOperationResult.Ok();
    }

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool HasMoneyScale(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
