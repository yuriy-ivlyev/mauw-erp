using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Application.Mappers;

public static class OrderPermittedActionsPolicy
{
    public const string ReopenForAdjustmentAction = "ReopenForAdjustment";
    public const string SyncAndFinalizeInvoiceAction = "SyncAndFinalizeInvoice";

    /// <summary>
    /// Lines are editable on drafts and, for a completed order, only while it is reopened for a
    /// pre-SDI adjustment whose invoice has not left for the SDI yet.
    /// </summary>
    public static bool IsReadOnly(Order order, Invoice? invoice = null)
    {
        if (order.Status == OrderStatus.Draft)
            return false;

        return !(order.IsUnderAdjustment && order.Status == OrderStatus.Completed &&
                 (invoice is null || invoice.CanBeAdjustedBeforeSdi));
    }

    public static List<OrderPermittedActionDto> GetFor(Order order, Invoice? invoice = null)
    {
        var id = order.Id;
        var endpoint = $"/api/v1/orders/{id}";
        var actions = new List<OrderPermittedActionDto>();

        void Add(
            string action,
            string label,
            string path,
            string style,
            bool requiresDialog = false,
            string? dialogType = null) =>
            actions.Add(new(
                action,
                label,
                $"{endpoint}/{path}",
                "POST",
                style,
                requiresDialog,
                dialogType));

        switch (order.Status)
        {
            case OrderStatus.Draft:
                if (order.FulfillmentType == OrderFulfillmentType.CashDesk)
                    Add("FinalizeCashSale", "Finalizza Vendita Cassa", "finalize-cash-sale",
                        "primary", true, "PaymentDetails");
                else
                    Add("Confirm", "Conferma Ordine", "confirm", "primary");
                Add("Cancel", "Annulla Bozza", "cancel", "danger", true, "CancelReason");
                break;

            case OrderStatus.PendingApproval:
                Add("Confirm", "Approva Fido e Conferma", "approve", "primary");
                Add("Cancel", "Rifiuta / Annulla", "cancel", "danger", true, "CancelReason");
                break;

            case OrderStatus.Confirmed:
                Add("StartPicking", "Prendi in Carico Magazzino", "start-picking", "primary");
                Add("PutOnHold", "Metti in Sospeso", "hold", "warning", true, "CancelReason");
                Add("Cancel", "Annulla Ordine", "cancel", "danger", true, "CancelReason");
                break;

            case OrderStatus.Picking:
                Add("Ship", "Prepara Spedizione / Emetti DDT", "ship",
                    "primary", true, "ShipmentDetails");
                Add("PutOnHold", "Metti in Sospeso", "hold", "warning", true, "CancelReason");
                Add("Cancel", "Annulla Ordine", "cancel", "danger", true, "CancelReason");
                break;

            case OrderStatus.Packing:
                Add("Ship", "Prepara Spedizione / Emetti DDT", "ship",
                    "primary", true, "ShipmentDetails");
                Add("PutOnHold", "Metti in Sospeso", "hold", "warning", true, "CancelReason");
                Add("Cancel", "Annulla Ordine", "cancel", "danger", true, "CancelReason");
                break;

            case OrderStatus.PartiallyShipped:
                Add("Ship", "Nuova Spedizione Residuo", "ship",
                    "primary", true, "ShipmentDetails");
                Add("CancelRemainingLines", "Stralcia Residuo (Saldo)", "cancel-remaining",
                    "warning", true, "CancelReason");
                Add("PutOnHold", "Metti in Sospeso", "hold", "secondary", true, "CancelReason");
                break;

            case OrderStatus.Shipped:
                Add("Complete", "Chiudi / Completa Ordine", "complete", "primary");
                break;

            case OrderStatus.OnHold:
                Add("Resume", "Ripristina Lavorazione", "resume", "primary");
                if (order.StatusBeforeHold != OrderStatus.PartiallyShipped)
                    Add("Cancel", "Annulla Ordine Definitivamente", "cancel",
                        "danger", true, "CancelReason");
                break;

            case OrderStatus.Completed when invoice is { CanBeAdjustedBeforeSdi: true }:
                if (order.IsUnderAdjustment)
                    Add(SyncAndFinalizeInvoiceAction, "Aggiorna Fattura e Chiudi", "sync-invoice", "primary");
                else
                    Add(ReopenForAdjustmentAction, "Modifica Righe (Pre-SDI)", "reopen-adjustment", "warning");
                break;
        }

        return actions;
    }

    public static string GetStatusDescription(OrderStatus status) => status switch
    {
        OrderStatus.Draft => "Bozza",
        OrderStatus.PendingApproval => "In attesa di approvazione",
        OrderStatus.Confirmed => "Confermato",
        OrderStatus.Picking => "In prelievo",
        OrderStatus.Packing => "In imballaggio",
        OrderStatus.Shipped => "Spedito",
        OrderStatus.PartiallyShipped => "Parzialmente spedito",
        OrderStatus.Delivered => "Consegnato",
        OrderStatus.Completed => "Completato",
        OrderStatus.Cancelled => "Annullato",
        OrderStatus.OnHold => "In sospeso",
        OrderStatus.Suspended => "Sospeso",
        OrderStatus.Terminated => "Terminato",
        _ => status.ToString()
    };
}
