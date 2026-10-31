using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Sales order aggregate root, including the cash-desk and shipping lifecycles.
/// </summary>
public class Order : Entity<Guid>
{
    private readonly List<Shipment> _shipments = [];

    private static readonly IReadOnlyDictionary<OrderStatus, HashSet<OrderStatus>> ValidTransitions =
        new Dictionary<OrderStatus, HashSet<OrderStatus>>
        {
            [OrderStatus.Draft] =
            [
                OrderStatus.PendingApproval, OrderStatus.Confirmed, OrderStatus.Completed,
                OrderStatus.Cancelled, OrderStatus.OnHold, OrderStatus.Suspended, OrderStatus.Terminated
            ],
            [OrderStatus.PendingApproval] =
            [
                OrderStatus.Draft, OrderStatus.Confirmed, OrderStatus.Cancelled, OrderStatus.OnHold
            ],
            [OrderStatus.Confirmed] =
            [
                OrderStatus.Picking, OrderStatus.Cancelled, OrderStatus.OnHold
            ],
            [OrderStatus.Picking] =
            [
                OrderStatus.Packing, OrderStatus.Cancelled, OrderStatus.OnHold
            ],
            [OrderStatus.Packing] =
            [
                OrderStatus.Shipped, OrderStatus.PartiallyShipped, OrderStatus.Cancelled, OrderStatus.OnHold
            ],
            [OrderStatus.PartiallyShipped] =
            [
                OrderStatus.PartiallyShipped, OrderStatus.Shipped, OrderStatus.Completed, OrderStatus.OnHold
            ],
            [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Completed, OrderStatus.OnHold],
            [OrderStatus.Delivered] = [OrderStatus.Completed, OrderStatus.OnHold],
            [OrderStatus.OnHold] =
            [
                OrderStatus.Draft, OrderStatus.PendingApproval, OrderStatus.Confirmed,
                OrderStatus.Picking, OrderStatus.Packing, OrderStatus.Shipped, OrderStatus.PartiallyShipped,
                OrderStatus.Delivered, OrderStatus.Suspended, OrderStatus.Cancelled
            ],
            [OrderStatus.Suspended] =
            [
                OrderStatus.Suspended, OrderStatus.Draft, OrderStatus.Confirmed,
                OrderStatus.Terminated, OrderStatus.OnHold
            ],
            [OrderStatus.Terminated] = [],
            [OrderStatus.Completed] = [],
            [OrderStatus.Cancelled] = []
        };

    public Guid ContactId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public DateTime OrderDate { get; set; }
    public decimal CustomerDiscountRate { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal ShippingCost { get; private set; } = 0.00m;
    public decimal? ShippingVatRate { get; private set; }
    public string? ShippingNatureCode { get; private set; }
    public string? InternalNotes { get; private set; }
    public string? TerminalId { get; private set; }
    public string? DocumentReference { get; private set; }
    public OrderType OrderType { get; set; } = OrderType.Standard;
    public OrderFulfillmentType FulfillmentType { get; private set; }
    public Guid? FulfillmentWarehouseId { get; private set; }
    public Guid? WarehouseOperatorId { get; private set; }
    public int? BoxCount { get; private set; }
    public decimal? WeightKg { get; private set; }
    public string? CarrierName { get; private set; }
    public string? TrackingCode { get; private set; }
    public string? DdtNumber { get; private set; }
    public string? PaymentMethod { get; private set; }
    public string? PaymentTransactionDetails { get; private set; }
    public string? ApprovalReason { get; private set; }
    public string? HoldReason { get; private set; }
    public OrderStatus? StatusBeforeHold { get; private set; }

    /// <summary>
    /// A completed order temporarily reopened to adjust lines before its invoice is transmitted to the SDI.
    /// The status stays <see cref="OrderStatus.Completed"/>; only lines become editable.
    /// </summary>
    public bool IsUnderAdjustment { get; private set; }
    public DateTime? AdjustmentStartedAt { get; private set; }
    public string? AdjustmentStartedBy { get; private set; }
    public Contact Contact { get; set; } = null!;
    public Warehouse? FulfillmentWarehouse { get; private set; }
    public ICollection<OrderItem> Items { get; private set; } = [];
    public IReadOnlyCollection<Shipment> Shipments => _shipments;

    public Order(
        Guid contactId,
        string orderNumber,
        decimal customerDiscountRate = 0,
        OrderFulfillmentType fulfillmentType = OrderFulfillmentType.StandardShipping)
    {
        Id = Guid.NewGuid();
        ContactId = contactId;
        OrderNumber = orderNumber;
        Status = OrderStatus.Draft;
        OrderDate = DateTime.UtcNow;
        CustomerDiscountRate = customerDiscountRate;
        FulfillmentType = fulfillmentType;
        TaxableTotal = 0;
        VatTotal = 0;
        GrossTotal = 0;
    }

    private Order() { }

    public void AddShipment(Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        if (shipment.OrderId != Id)
            throw new InvalidOperationException("The shipment belongs to another order.");
        if (_shipments.Any(existing => existing.Id == shipment.Id))
            throw new InvalidOperationException("The shipment is already associated with this order.");

        _shipments.Add(shipment);
        UpdateTimestamp();
    }

    public void SetInternalNotes(string? notes)
    {
        InternalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public AccountingResult<CalculatedDocument> AddItem(OrderItem item, ItalianAccountingCalculator calculator)
    {
        EnsureEditable();
        var result = calculator.CalculateAndApply(GetAccountingLines(Items.Append(item)));
        if (!result.Success)
            return result;

        Items.Add(item);
        ApplyTotals(result.Data!);
        return result;
    }

    public AccountingResult<CalculatedDocument> AddCompletedBillingItem(
        OrderItem item, ItalianAccountingCalculator calculator)
    {
        if (Status != OrderStatus.Completed)
            return AccountingResult<CalculatedDocument>.Fail("Le righe contabili aggiuntive sono ammesse solo su ordini completati.");
        if (IsUnderAdjustment)
            return AccountingResult<CalculatedDocument>.Fail(AdjustmentInProgressMessage);
        if (item.OrderId != Id)
            return AccountingResult<CalculatedDocument>.Fail("La riga appartiene a un altro ordine.");

        item.MarkBillingOnly();
        var result = calculator.CalculateAndApply(GetAccountingLines(Items.Append(item)));
        if (!result.Success)
            return result;

        Items.Add(item);
        ApplyTotals(result.Data!);
        return result;
    }

    public AccountingResult<CalculatedDocument> ExcludeCompletedBillingItem(
        Guid itemId, ItalianAccountingCalculator calculator)
    {
        if (Status != OrderStatus.Completed)
            return AccountingResult<CalculatedDocument>.Fail("Le righe possono essere rimosse dalla fattura solo su ordini completati.");
        if (IsUnderAdjustment)
            return AccountingResult<CalculatedDocument>.Fail(AdjustmentInProgressMessage);
        var item = Items.FirstOrDefault(line => line.Id == itemId && !line.IsExcludedFromInvoice);
        if (item is null)
            return AccountingResult<CalculatedDocument>.Fail("Riga ordine non trovata.");

        var remainingItems = Items.Where(line => line.Id != itemId && !line.IsExcludedFromInvoice).ToList();
        if (remainingItems.Count == 0 && ShippingCost == 0m)
            return AccountingResult<CalculatedDocument>.Fail("L'ordine deve conservare almeno una riga fatturabile o una spesa di spedizione.");

        var result = calculator.CalculateAndApply(GetAccountingLines(remainingItems));
        if (!result.Success)
            return result;

        item.ExcludeFromInvoice();
        ApplyTotals(result.Data!);
        return result;
    }

    private const string AdjustmentInProgressMessage =
        "L'ordine è in rettifica pre-SDI: usare il flusso di modifica righe e aggiornare la fattura.";

    /// <summary>Reopens a completed order so its lines can be adjusted before the invoice reaches the SDI.</summary>
    public OrderWorkflowResult BeginAdjustment(string? startedBy, DateTime? startedAt = null)
    {
        if (Status != OrderStatus.Completed)
            return OrderWorkflowResult.Fail("Solo un ordine completato può essere riaperto per variazione.");
        if (IsUnderAdjustment)
            return OrderWorkflowResult.Fail("L'ordine è già in rettifica.");

        IsUnderAdjustment = true;
        AdjustmentStartedAt = startedAt ?? DateTime.UtcNow;
        AdjustmentStartedBy = string.IsNullOrWhiteSpace(startedBy) ? null : startedBy.Trim();
        UpdateTimestamp();
        return OrderWorkflowResult.Ok();
    }

    /// <summary>Closes the adjustment: the order returns to its plain completed state.</summary>
    public OrderWorkflowResult CompleteAdjustment()
    {
        if (!IsUnderAdjustment)
            return OrderWorkflowResult.Fail("L'ordine non è in rettifica.");

        IsUnderAdjustment = false;
        AdjustmentStartedAt = null;
        AdjustmentStartedBy = null;
        UpdateTimestamp();
        return OrderWorkflowResult.Ok();
    }

    /// <summary>
    /// Replaces the adjustable lines of an order under adjustment: lines missing from
    /// <paramref name="keptLines"/> are removed, listed lines get the new quantity and
    /// <paramref name="addedItems"/> are appended. Nothing changes when the result is a failure.
    /// </summary>
    public AccountingResult<CalculatedDocument> AdjustLines(
        IReadOnlyCollection<OrderLineQuantityChange> keptLines,
        IReadOnlyCollection<OrderItem> addedItems,
        ItalianAccountingCalculator calculator)
    {
        if (Status != OrderStatus.Completed || !IsUnderAdjustment)
            return AccountingResult<CalculatedDocument>.Fail("Le righe sono modificabili solo su un ordine riaperto per rettifica.");
        if (keptLines.Any(line => line is null) || addedItems.Any(item => item is null))
            return AccountingResult<CalculatedDocument>.Fail("Una riga della rettifica non può essere null.");
        if (keptLines.Select(line => line.OrderItemId).Distinct().Count() != keptLines.Count)
            return AccountingResult<CalculatedDocument>.Fail("Una riga ordine è indicata più volte nella rettifica.");

        var currentItems = Items.Where(item => !item.IsExcludedFromInvoice).ToDictionary(item => item.Id);
        var inputs = new List<DocumentLineInput>();
        foreach (var kept in keptLines)
        {
            if (!currentItems.TryGetValue(kept.OrderItemId, out var current))
                return AccountingResult<CalculatedDocument>.Fail("Riga ordine non trovata.");
            if (current.IsCustomerReturnLine)
            {
                if (kept.Quantity != current.Quantity)
                    return AccountingResult<CalculatedDocument>.Fail("Le righe di reso non sono modificabili in rettifica.");
            }
            else if (kept.Quantity <= 0)
            {
                return AccountingResult<CalculatedDocument>.Fail("La quantità di una riga deve essere un intero maggiore di zero.");
            }

            inputs.Add(new DocumentLineInput(
                kept.Quantity, current.UnitNetPrice, current.VatRate, current.NatureCode, current.DiscountPercent));
        }

        foreach (var added in addedItems)
        {
            if (added.OrderId != Id)
                return AccountingResult<CalculatedDocument>.Fail("La riga appartiene a un altro ordine.");
            if (added.Quantity <= 0 || added.IsCustomerReturnLine)
                return AccountingResult<CalculatedDocument>.Fail("Le nuove righe devono avere una quantità positiva e non essere resi.");
            if (added.IsBillingOnly || added.IsExcludedFromInvoice)
                return AccountingResult<CalculatedDocument>.Fail("Le nuove righe di rettifica devono movimentare il magazzino.");

            inputs.Add(new DocumentLineInput(
                added.Quantity, added.UnitNetPrice, added.VatRate, added.NatureCode, added.DiscountPercent));
        }

        if (inputs.Count == 0 && ShippingCost == 0m)
            return AccountingResult<CalculatedDocument>.Fail("L'ordine deve conservare almeno una riga fatturabile o una spesa di spedizione.");
        if (ShippingCost > 0m && ShippingVatRate.HasValue)
            inputs.Add(new DocumentLineInput(1m, ShippingCost, ShippingVatRate.Value, ShippingNatureCode));

        // Pure pre-check so that an invalid adjustment never leaves the aggregate half-modified.
        var validation = calculator.Calculate(inputs);
        if (!validation.Success)
            return validation;

        var keptIds = keptLines.ToDictionary(line => line.OrderItemId, line => line.Quantity);
        foreach (var removed in currentItems.Values.Where(item => !keptIds.ContainsKey(item.Id)).ToList())
            Items.Remove(removed);
        foreach (var item in Items.Where(item => keptIds.ContainsKey(item.Id)))
            item.AdjustQuantity(keptIds[item.Id]);
        foreach (var added in addedItems)
            Items.Add(added);

        var result = calculator.CalculateAndApply(GetAccountingLines(Items));
        if (!result.Success)
            return result;

        if (FulfillmentType == OrderFulfillmentType.StandardShipping)
        {
            foreach (var item in Items.Where(item => item.AffectsStock && item.Quantity > 0))
                item.AlignShippedQuantityWithQuantity();
        }

        ApplyTotals(result.Data!);
        return result;
    }

    public AccountingResult<CalculatedDocument> ReplaceItem(
        Guid itemId,
        OrderItem replacement,
        ItalianAccountingCalculator calculator)
    {
        EnsureEditable();
        var current = Items.FirstOrDefault(item => item.Id == itemId);
        if (current is null)
            return AccountingResult<CalculatedDocument>.Fail("Order item not found.");

        var result = calculator.CalculateAndApply(
            GetAccountingLines(Items.Where(item => item.Id != itemId).Append(replacement)));
        if (!result.Success)
            return result;

        Items.Remove(current);
        Items.Add(replacement);
        ApplyTotals(result.Data!);
        return result;
    }

    public AccountingResult<CalculatedDocument> RemoveItem(
        Guid itemId, ItalianAccountingCalculator calculator)
    {
        EnsureEditable();
        var item = Items.FirstOrDefault(line => line.Id == itemId);
        if (item is null)
            return AccountingResult<CalculatedDocument>.Fail("Order item not found.");

        var result = calculator.CalculateAndApply(GetAccountingLines(Items.Where(line => line.Id != itemId)));
        if (!result.Success)
            return result;

        Items.Remove(item);
        ApplyTotals(result.Data!);
        return result;
    }

    public AccountingResult<CalculatedDocument> UpdateTotals(ItalianAccountingCalculator calculator)
    {
        EnsureEditable();
        var result = calculator.CalculateAndApply(GetAccountingLines(Items));
        if (result.Success)
            ApplyTotals(result.Data!);
        return result;
    }

    public AccountingResult<CalculatedDocument> SetShippingCharge(
        decimal shippingCost,
        decimal? vatRate,
        string? natureCode,
        ItalianAccountingCalculator calculator)
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Completed))
            return AccountingResult<CalculatedDocument>.Fail("Il costo di spedizione può essere modificato solo su ordini in bozza o completati.");
        if (IsUnderAdjustment)
            return AccountingResult<CalculatedDocument>.Fail(AdjustmentInProgressMessage);
        if (shippingCost < 0m)
            return AccountingResult<CalculatedDocument>.Fail("Shipping cost cannot be negative.");
        if (decimal.Round(shippingCost, 2, MidpointRounding.AwayFromZero) != shippingCost)
            return AccountingResult<CalculatedDocument>.Fail("Shipping cost must have at most two decimal places.");
        if (shippingCost > 0m && !vatRate.HasValue)
            return AccountingResult<CalculatedDocument>.Fail("VAT rate is required for a shipping charge.");

        var effectiveVatRate = shippingCost > 0m ? vatRate : null;
        var effectiveNatureCode = shippingCost > 0m ? natureCode : null;
        var lines = GetAccountingLines(Items, shippingCost, effectiveVatRate, effectiveNatureCode);
        var result = calculator.CalculateAndApply(lines);
        if (!result.Success)
            return result;

        ShippingCost = shippingCost;
        ShippingVatRate = effectiveVatRate;
        ShippingNatureCode = effectiveNatureCode;
        ApplyTotals(result.Data!);
        return result;
    }

    private IEnumerable<ICommercialDocumentLine> GetAccountingLines(
        IEnumerable<OrderItem> items,
        decimal? shippingCost = null,
        decimal? shippingVatRate = null,
        string? shippingNatureCode = null)
    {
        var lines = items.Where(item => !item.IsExcludedFromInvoice)
            .Cast<ICommercialDocumentLine>().ToList();
        var amount = shippingCost ?? ShippingCost;
        var rate = shippingCost.HasValue ? shippingVatRate : ShippingVatRate;
        var nature = shippingCost.HasValue ? shippingNatureCode : ShippingNatureCode;
        if (amount > 0m && rate.HasValue)
            lines.Add(new ShippingChargeLine(amount, rate.Value, nature));
        return lines;
    }

    private void ApplyTotals(CalculatedDocument document)
    {
        TaxableTotal = document.TotalTaxable;
        VatTotal = document.TotalVat;
        GrossTotal = document.TotalGross;
        UpdateTimestamp();
    }

    private sealed class ShippingChargeLine : ICommercialDocumentLine
    {
        public decimal Quantity => 1m;
        public decimal UnitNetPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal VatRate { get; }
        public string? NatureCode { get; set; }
        public decimal LineNetTotal { get; set; }
        public decimal? LineVatTotal { get; set; }
        public decimal? LineGrossTotal { get; set; }

        public ShippingChargeLine(decimal unitNetPrice, decimal vatRate, string? natureCode)
        {
            UnitNetPrice = unitNetPrice;
            VatRate = vatRate;
            NatureCode = natureCode;
        }
    }

    public void SetFulfillmentWarehouse(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            throw new InvalidOperationException("A fulfillment warehouse is required.");
        if (Status is not (OrderStatus.Draft or OrderStatus.PendingApproval))
            throw new InvalidOperationException("The fulfillment warehouse can only be set on an open order.");

        FulfillmentWarehouseId = warehouseId;
        UpdateTimestamp();
    }

    public void RequireApproval(string reason)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Only a draft order can be sent for approval.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An approval reason is required.", nameof(reason));

        EnsureHasLines();
        TransitionTo(OrderStatus.PendingApproval);
        ApprovalReason = reason.Trim();
        UpdateTimestamp();
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Only a draft order can be confirmed.");
        if (FulfillmentType != OrderFulfillmentType.StandardShipping)
            throw new InvalidOperationException("Cash-desk orders must be finalized through FinalizeCashSale.");

        EnsureHasLines();
        ConfirmAndRaiseEvent();
    }

    public void Approve()
    {
        if (Status != OrderStatus.PendingApproval)
            throw new InvalidOperationException("Only an order pending approval can be approved.");
        if (FulfillmentType != OrderFulfillmentType.StandardShipping)
            throw new InvalidOperationException("Cash-desk orders do not use shipping approval.");

        EnsureHasLines();
        ApprovalReason = null;
        ConfirmAndRaiseEvent();
    }

    private void ConfirmAndRaiseEvent()
    {
        TransitionTo(OrderStatus.Confirmed);
    }

    public void FinalizeCashSale(string paymentMethod, string? transactionDetails)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("A cash sale can only be finalized from Draft.");
        if (FulfillmentType != OrderFulfillmentType.CashDesk)
            throw new InvalidOperationException("Only cash-desk orders can be finalized as a cash sale.");
        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new ArgumentException("A payment method is required.", nameof(paymentMethod));
        if (paymentMethod.Length > 50)
            throw new ArgumentException("Payment method must be at most 50 characters.", nameof(paymentMethod));
        if (transactionDetails?.Length > 500)
            throw new ArgumentException("Transaction details must be at most 500 characters.", nameof(transactionDetails));

        EnsureHasLines();
        TransitionTo(OrderStatus.Completed);
        PaymentMethod = paymentMethod.Trim();
        PaymentTransactionDetails = string.IsNullOrWhiteSpace(transactionDetails)
            ? null
            : transactionDetails.Trim();
        UpdateTimestamp();
    }

    public void StartPicking(Guid warehouseOperatorId)
    {
        if (warehouseOperatorId == Guid.Empty)
            throw new ArgumentException("A warehouse operator is required.", nameof(warehouseOperatorId));
        EnsureShippingOrder();
        TransitionTo(OrderStatus.Picking);
        WarehouseOperatorId = warehouseOperatorId;
        UpdateTimestamp();
    }

    public void CompletePacking(int boxCount, decimal weightKg)
    {
        EnsureShippingOrder();
        if (boxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(boxCount), "At least one box is required.");
        if (weightKg < 0)
            throw new ArgumentOutOfRangeException(nameof(weightKg), "Weight cannot be negative.");

        TransitionTo(OrderStatus.Packing);
        BoxCount = boxCount;
        WeightKg = weightKg;
        UpdateTimestamp();
    }

    public void Ship(string carrierName, string? trackingCode, string ddtNumber, bool allRemainingItemsShipped = true)
    {
        EnsureShippingOrder();
        if (string.IsNullOrWhiteSpace(carrierName))
            throw new ArgumentException("A carrier name is required.", nameof(carrierName));
        if (string.IsNullOrWhiteSpace(ddtNumber))
            throw new ArgumentException("A DDT number is required.", nameof(ddtNumber));
        if (!FulfillmentWarehouseId.HasValue)
            throw new InvalidOperationException("The order has no fulfillment warehouse.");

        if (Status is not (OrderStatus.Packing or OrderStatus.PartiallyShipped))
            throw new InvalidOperationException("Only a packed or partially shipped order can be shipped.");

        TransitionTo(allRemainingItemsShipped ? OrderStatus.Shipped : OrderStatus.PartiallyShipped);
        CarrierName = carrierName.Trim();
        TrackingCode = string.IsNullOrWhiteSpace(trackingCode) ? null : trackingCode.Trim();
        DdtNumber = ddtNumber.Trim();
        UpdateTimestamp();
    }

    public void MarkDelivered()
    {
        EnsureShippingOrder();
        TransitionTo(OrderStatus.Delivered);
    }

    public void Complete()
    {
        EnsureShippingOrder();
        TransitionTo(OrderStatus.Completed);
    }

    public void CancelRemainingLines(string reason)
    {
        if (Status != OrderStatus.PartiallyShipped)
            throw new InvalidOperationException("Only a partially shipped order can close its remaining lines.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A cancellation reason is required.", nameof(reason));

        TransitionTo(OrderStatus.Completed);
    }

    public void Cancel(string reason)
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Confirmed or
            OrderStatus.PendingApproval or OrderStatus.Picking or OrderStatus.Packing or OrderStatus.OnHold))
            throw new InvalidOperationException($"An order in status {Status} cannot be cancelled.");
        if (Status == OrderStatus.OnHold && StatusBeforeHold == OrderStatus.PartiallyShipped)
            throw new InvalidOperationException("A partially shipped order cannot be cancelled as a whole.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A cancellation reason is required.", nameof(reason));

        TransitionTo(OrderStatus.Cancelled);
    }

    public void PlaceOnHold(string reason)
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled or
            OrderStatus.Terminated or OrderStatus.OnHold)
            throw new InvalidOperationException($"An order in status {Status} cannot be put on hold.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A hold reason is required.", nameof(reason));

        var previousStatus = Status;
        TransitionTo(OrderStatus.OnHold);
        StatusBeforeHold = previousStatus;
        HoldReason = reason.Trim();
        UpdateTimestamp();
    }

    public void ReleaseHold()
    {
        if (Status != OrderStatus.OnHold || !StatusBeforeHold.HasValue)
            throw new InvalidOperationException("The order is not on hold.");

        var previousStatus = StatusBeforeHold.Value;
        TransitionTo(previousStatus);
        StatusBeforeHold = null;
        HoldReason = null;
        UpdateTimestamp();
    }

    public void Suspend(string? terminalId)
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Suspended))
            throw new InvalidOperationException($"An order in status {Status} cannot be suspended.");

        TransitionTo(OrderStatus.Suspended);
        TerminalId = terminalId;
        UpdateTimestamp();
    }

    public void Resume(string? terminalId)
    {
        TransitionTo(OrderStatus.Draft);
        TerminalId = terminalId;
        UpdateTimestamp();
    }

    public void ConfirmCassaSession()
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Suspended))
            throw new InvalidOperationException("Only an open Cassa session can be confirmed.");
        EnsureHasLines();
        if (OrderType != OrderType.CassaSession)
            throw new InvalidOperationException("Only legacy Cassa sessions can use this confirmation path.");
        TransitionTo(OrderStatus.Confirmed);
    }

    public void Terminate()
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Suspended))
            throw new InvalidOperationException($"An order in status {Status} cannot be terminated.");

        TransitionTo(OrderStatus.Terminated);
    }

    public void SetDocumentReference(string? docRef)
    {
        DocumentReference = docRef;
        UpdateTimestamp();
    }

    public void ClearItems()
    {
        EnsureEditable();
        Items.Clear();
        TaxableTotal = 0;
        VatTotal = 0;
        GrossTotal = 0;
        UpdateTimestamp();
    }

    private void EnsureEditable()
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Suspended))
            throw new InvalidOperationException($"Order lines cannot be modified in status {Status}.");
    }

    private void EnsureShippingOrder()
    {
        if (FulfillmentType != OrderFulfillmentType.StandardShipping)
            throw new InvalidOperationException("This transition is only valid for shipping orders.");
    }

    private void EnsureHasLines()
    {
        if (Items.Count == 0)
            throw new InvalidOperationException("Cannot process an order without items.");
    }

    private void TransitionTo(OrderStatus nextStatus)
    {
        EnsureValidTransition(Status, nextStatus);
        if (Status == nextStatus)
            return;

        Status = nextStatus;
        UpdateTimestamp();
    }

    private static void EnsureValidTransition(OrderStatus current, OrderStatus next)
    {
        if (!ValidTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
            throw new InvalidOperationException($"Invalid order status transition: {current} -> {next}.");
    }
}
