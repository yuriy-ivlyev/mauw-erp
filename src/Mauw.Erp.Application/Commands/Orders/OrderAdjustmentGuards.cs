using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Application.Commands.Orders;

internal static class OrderAdjustmentGuards
{
    public static string? EnsureInvoiceAdjustable(Invoice? invoice)
    {
        if (invoice is null)
            return "L'ordine non ha una fattura da rettificare: generare prima la fattura.";
        if (invoice.CanBeAdjustedBeforeSdi)
            return null;

        return invoice.SdiStatus == SdiStatus.Pending
            ? "La fattura è in coda per l'invio allo SDI e l'ordine non può essere modificato."
            : "La fattura è già stata inviata allo SDI e l'ordine non può essere modificato.";
    }

    public static string? EnsureUnderAdjustment(Order order) =>
        order.IsUnderAdjustment && order.Status == OrderStatus.Completed
            ? null
            : "L'ordine non è in rettifica: riaprirlo prima di modificarne le righe.";
}
