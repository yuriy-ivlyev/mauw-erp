using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;

namespace Mauw.Erp.Application.Services;

internal static class CustomerCreditPolicy
{
    public static async Task<string?> GetApprovalReasonAsync(IUnitOfWork unitOfWork, Order order)
    {
        var contact = await unitOfWork.Contacts.GetByIdWithDetailsAsync(order.ContactId);
        var profile = contact?.CustomerProfile;
        if (profile is null)
            return null;
        if (profile.IsBlocked)
            return string.IsNullOrWhiteSpace(profile.BlockReason)
                ? "Il cliente è bloccato e richiede approvazione."
                : $"Cliente bloccato: {profile.BlockReason}";

        if (profile.CreditLimit <= 0m)
            return null;

        var invoices = (await unitOfWork.Invoices.GetByContactIdAsync(order.ContactId)).ToList();
        var invoiceIds = invoices.Select(invoice => invoice.Id).ToList();
        List<CreditNote> creditNotes = invoiceIds.Count == 0
            ? []
            : (await unitOfWork.CreditNotes.GetByInvoiceIdsAsync(invoiceIds)).ToList();
        var creditsByInvoiceId = creditNotes
            .GroupBy(creditNote => creditNote.InvoiceId)
            .ToDictionary(group => group.Key, group => group.Sum(creditNote => creditNote.GrossTotal));

        var invoiceExposure = invoices.Sum(invoice =>
        {
            creditsByInvoiceId.TryGetValue(invoice.Id, out var creditTotal);
            return Math.Max(0m, invoice.GrossTotal - invoice.PaidAmount - creditTotal);
        });

        var invoicedOrderIds = invoices
            .Where(invoice => invoice.OrderId.HasValue)
            .Select(invoice => invoice.OrderId!.Value)
            .ToHashSet();
        var activeOrderExposure = (await unitOfWork.Orders.GetByContactIdAsync(order.ContactId))
            .Where(existingOrder =>
                existingOrder.Id != order.Id &&
                existingOrder.FulfillmentType == OrderFulfillmentType.StandardShipping &&
                !invoicedOrderIds.Contains(existingOrder.Id) &&
                existingOrder.Status is (OrderStatus.Confirmed or OrderStatus.Picking or
                    OrderStatus.Packing or OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.OnHold))
            .Sum(existingOrder => existingOrder.GrossTotal);

        var exposure = invoiceExposure + activeOrderExposure + order.GrossTotal;
        return exposure > profile.CreditLimit
            ? $"Esposizione cliente {exposure:0.00} superiore al fido disponibile {profile.CreditLimit:0.00}."
            : null;
    }
}
