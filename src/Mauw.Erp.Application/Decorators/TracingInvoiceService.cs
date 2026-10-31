using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Decorators;

public sealed class TracingInvoiceService(IInvoiceService inner) : TracingDecoratorBase, IInvoiceService
{
    public Task<OperationResult<InvoiceDto>> GetInvoiceAsync(Guid id) =>
        TraceAsync("Invoice.Get", () => inner.GetInvoiceAsync(id),
            a => a?.SetTag("invoice.id", id));

    public Task<OperationResult<InvoiceDto>> GenerateFromOrderAsync(Guid orderId) =>
        TraceAsync("Invoice.GenerateFromOrder", () => inner.GenerateFromOrderAsync(orderId),
            a => a?.SetTag("order.id", orderId));

    public Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesAsync(
        Guid? contactId,
        string? search,
        int page,
        int pageSize,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        bool unpaidOnly = false,
        string? sortBy = null,
        bool sortDescending = true) =>
        TraceAsync("Invoice.List", () => inner.GetInvoicesAsync(
                contactId, search, page, pageSize, status, dateFrom, dateTo, unpaidOnly, sortBy, sortDescending),
            a =>
            {
                if (contactId.HasValue)
                    a?.SetTag("contact.id", contactId.Value);
                a?.SetTag("invoice.search", search);
                a?.SetTag("invoice.status", status);
                a?.SetTag("invoice.date_from", dateFrom?.ToString("yyyy-MM-dd"));
                a?.SetTag("invoice.date_to", dateTo?.ToString("yyyy-MM-dd"));
                a?.SetTag("invoice.unpaid_only", unpaidOnly);
                a?.SetTag("invoice.sort_by", sortBy);
                a?.SetTag("invoice.sort_descending", sortDescending);
                a?.SetTag("pagination.page", page);
                a?.SetTag("pagination.page_size", pageSize);
            });

    public Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesByContactAsync(Guid contactId, int page, int pageSize) =>
        TraceAsync("Invoice.GetByContact", () => inner.GetInvoicesByContactAsync(contactId, page, pageSize),
            a => a?.SetTag("contact.id", contactId));

    public Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request) =>
        TraceAsync("Invoice.RecordPayment", () => inner.RecordPaymentAsync(invoiceId, request),
            a => a?.SetTag("invoice.id", invoiceId));

    public Task<OperationResult<byte[]>> GetInvoicePdfAsync(Guid id) =>
        TraceAsync("Invoice.Pdf", () => inner.GetInvoicePdfAsync(id),
            a => a?.SetTag("invoice.id", id));

    public Task<OperationResult<string>> GetInvoiceXmlAsync(Guid id) =>
        TraceAsync("Invoice.Xml", () => inner.GetInvoiceXmlAsync(id),
            a => a?.SetTag("invoice.id", id));
}
