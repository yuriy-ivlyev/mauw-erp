using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Invoice entity representing a customer invoice.
/// Can be immediate or deferred from DDTs.
/// </summary>
public class Invoice : Entity<Guid>
{
    public Guid ContactId { get; set; }
    public Guid? OrderId { get; set; }
    public string InvoiceNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal PaidAmount { get; set; } = 0.00m;
    public decimal ShippingCost { get; private set; } = 0.00m;
    public decimal? ShippingVatRate { get; private set; }
    public string? ShippingNatureCode { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? SdiSubmittedAt { get; private set; }

    private SdiStatus _sdiStatus = SdiStatus.NotTransmitted;

    /// <summary>
    /// Transmission state; legacy rows that only carry <see cref="SdiSubmittedAt"/> are reported as transmitted.
    /// </summary>
    public SdiStatus SdiStatus => SdiSubmittedAt.HasValue && _sdiStatus < SdiStatus.Transmitted
        ? SdiStatus.Transmitted
        : _sdiStatus;

    /// <summary>True once the document has left for the SDI: its content is frozen.</summary>
    public bool IsSdiSubmitted => SdiStatus >= SdiStatus.Transmitted;

    /// <summary>Only a never-submitted invoice can be re-synchronized from a reopened order.</summary>
    public bool CanBeAdjustedBeforeSdi => SdiStatus == SdiStatus.NotTransmitted;

    public string Status => GrossTotal > 0m && PaidAmount >= GrossTotal
        ? InvoiceStatuses.Paid
        : PaidAmount > 0m
            ? InvoiceStatuses.PartiallyPaid
            : ConfirmedAt.HasValue
                ? InvoiceStatuses.Confirmed
                : InvoiceStatuses.Draft;

    // Navigation
    public Contact Contact { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<AgentCommission> Commissions { get; set; } = [];
    public ICollection<CreditNote> CreditNotes { get; set; } = [];

    public Invoice(
        Guid contactId,
        string invoiceNumber
    ) : base()
    {
        ContactId = contactId;
        InvoiceNumber = invoiceNumber;
        InvoiceDate = DateTime.UtcNow;
        TaxableTotal = 0.00m;
        VatTotal = 0.00m;
        GrossTotal = 0.00m;
    }

    /// <summary>
    /// Adds an item to the invoice and updates totals.
    /// </summary>
    public AccountingResult<CalculatedDocument> AddItem(InvoiceItem item, ItalianAccountingCalculator calculator)
    {
        if (IsSdiSubmitted)
            return AccountingResult<CalculatedDocument>.Fail("Una fattura inviata allo SDI non può essere modificata.");

        var result = calculator.CalculateAndApply(GetAccountingLines(Items.Append(item)));
        if (!result.Success)
            return result;
        Items.Add(item);
        ApplyTotals(result.Data!);
        return result;
    }

    /// <summary>
    /// Recalculates invoice totals from items.
    /// </summary>
    public AccountingResult<CalculatedDocument> UpdateTotals(ItalianAccountingCalculator calculator)
    {
        if (IsSdiSubmitted)
            return AccountingResult<CalculatedDocument>.Fail("I totali di una fattura inviata allo SDI non possono essere modificati.");

        var result = calculator.CalculateAndApply(GetAccountingLines(Items));
        if (result.Success)
            ApplyTotals(result.Data!);
        return result;
    }

    public AccountingResult<CalculatedDocument> Confirm(
        ItalianAccountingCalculator calculator, DateTime? confirmedAt = null)
    {
        if (ConfirmedAt.HasValue)
            return AccountingResult<CalculatedDocument>.Fail("La fattura è già stata confermata.");
        if (Items.Count == 0 && ShippingCost == 0m)
            return AccountingResult<CalculatedDocument>.Fail("Aggiungere almeno una riga prima di confermare la fattura.");

        var result = calculator.CalculateAndApply(GetAccountingLines(Items));
        if (!result.Success)
            return result;

        ApplyTotals(result.Data!);
        ConfirmedAt = confirmedAt ?? DateTime.UtcNow;
        return result;
    }

    public AccountingResult<CalculatedDocument> SyncFromOrder(
        Order order, ItalianAccountingCalculator calculator)
    {
        if (order.Status != OrderStatus.Completed)
            return AccountingResult<CalculatedDocument>.Fail("Solo un ordine completato può essere sincronizzato con una fattura.");
        if (OrderId != order.Id)
            return AccountingResult<CalculatedDocument>.Fail("L'ordine non corrisponde alla fattura.");
        if (IsSdiSubmitted)
            return AccountingResult<CalculatedDocument>.Fail("La fattura inviata allo SDI non può essere modificata.");

        var replacementItems = order.Items
            .Where(item => !item.IsExcludedFromInvoice)
            .Select(item => new InvoiceItem(
                Id, item.VariantId, item.Quantity, item.UnitNetPrice, item.VatRate,
                item.NatureCode, item.DiscountPercent)
            {
                PricingSnapshot = item.PricingSnapshot is null
                    ? null
                    : item.PricingSnapshot with { Discounts = item.PricingSnapshot.Discounts?.ToList() },
                Variant = item.Variant
            })
            .ToList();

        if (replacementItems.Count == 0 && order.ShippingCost == 0m)
            return AccountingResult<CalculatedDocument>.Fail(
                "L'ordine deve contenere almeno una riga fatturabile o una spesa di spedizione.");

        var result = calculator.CalculateAndApply(
            GetAccountingLines(replacementItems, order.ShippingCost, order.ShippingVatRate, order.ShippingNatureCode));
        if (!result.Success)
            return result;
        if (result.Data!.TotalGross < PaidAmount)
            return AccountingResult<CalculatedDocument>.Fail(
                "La modifica ridurrebbe il totale della fattura sotto l'importo già pagato.");

        Items.Clear();
        foreach (var item in replacementItems)
            Items.Add(item);
        ShippingCost = order.ShippingCost;
        ShippingVatRate = order.ShippingVatRate;
        ShippingNatureCode = order.ShippingNatureCode;
        ApplyTotals(result.Data);
        return result;
    }

    public InvoiceWorkflowResult MarkSdiPending()
    {
        if (!ConfirmedAt.HasValue)
            return InvoiceWorkflowResult.Fail("Confermare la fattura prima di accodarla allo SDI.");
        if (SdiStatus != SdiStatus.NotTransmitted)
            return InvoiceWorkflowResult.Fail("La fattura è già stata accodata o inviata allo SDI.");

        _sdiStatus = SdiStatus.Pending;
        UpdateTimestamp();
        return InvoiceWorkflowResult.Ok();
    }

    public InvoiceWorkflowResult MarkSdiTransmitted(DateTime submittedAt)
    {
        if (!ConfirmedAt.HasValue)
            return InvoiceWorkflowResult.Fail("Confermare la fattura prima di trasmetterla allo SDI.");
        if (IsSdiSubmitted)
            return InvoiceWorkflowResult.Fail("La fattura è già stata inviata allo SDI.");

        SdiSubmittedAt = submittedAt;
        _sdiStatus = SdiStatus.Transmitted;
        UpdateTimestamp();
        return InvoiceWorkflowResult.Ok();
    }

    public InvoiceWorkflowResult MarkSdiAccepted()
    {
        if (SdiStatus != SdiStatus.Transmitted)
            return InvoiceWorkflowResult.Fail("Solo una fattura trasmessa può essere accettata dallo SDI.");

        _sdiStatus = SdiStatus.Accepted;
        UpdateTimestamp();
        return InvoiceWorkflowResult.Ok();
    }

    private IEnumerable<ICommercialDocumentLine> GetAccountingLines(
        IEnumerable<InvoiceItem> items,
        decimal? shippingCost = null,
        decimal? shippingVatRate = null,
        string? shippingNatureCode = null)
    {
        var lines = items.Cast<ICommercialDocumentLine>().ToList();
        var amount = shippingCost ?? ShippingCost;
        var rate = shippingCost.HasValue ? shippingVatRate : ShippingVatRate;
        var nature = shippingCost.HasValue ? shippingNatureCode : ShippingNatureCode;
        if (amount > 0m && rate.HasValue)
            lines.Add(new InvoiceShippingChargeLine(amount, rate.Value, nature));
        return lines;
    }

    private void ApplyTotals(CalculatedDocument document)
    {
        TaxableTotal = document.TotalTaxable;
        VatTotal = document.TotalVat;
        GrossTotal = document.TotalGross;
        UpdateTimestamp();
    }

    private sealed class InvoiceShippingChargeLine : ICommercialDocumentLine
    {
        public decimal Quantity => 1m;
        public decimal UnitNetPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal VatRate { get; }
        public string? NatureCode { get; set; }
        public decimal LineNetTotal { get; set; }
        public decimal? LineVatTotal { get; set; }
        public decimal? LineGrossTotal { get; set; }

        public InvoiceShippingChargeLine(decimal unitNetPrice, decimal vatRate, string? natureCode)
        {
            UnitNetPrice = unitNetPrice;
            VatRate = vatRate;
            NatureCode = natureCode;
        }
    }

    /// <summary>
    /// Records a payment and checks if commissions should be accrued.
    /// </summary>
    public InvoiceWorkflowResult RecordPayment(decimal amount)
    {
        if (Status == InvoiceStatuses.Draft)
            return InvoiceWorkflowResult.Fail("Confermare la fattura prima di registrare un pagamento.");
        if (amount <= 0m || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
            return InvoiceWorkflowResult.Fail("L'importo del pagamento deve essere positivo e avere al massimo due decimali.");
        if (GrossTotal <= 0m)
            return InvoiceWorkflowResult.Fail("Non è possibile pagare una fattura senza un totale positivo.");
        if (amount > GrossTotal - PaidAmount)
            return InvoiceWorkflowResult.Fail("L'importo del pagamento supera il residuo della fattura.");

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

        return InvoiceWorkflowResult.Ok();
    }
}
