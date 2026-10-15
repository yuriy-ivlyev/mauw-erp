using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Decorators;

public sealed class TracingInvoiceService(IInvoiceService inner) : TracingDecoratorBase, IInvoiceService
{
    public Task<OperationResult<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request) =>
        TraceAsync("Invoice.Create", () => inner.CreateInvoiceAsync(request),
            a => a?.SetTag("contact.id", request.ContactId));

    public Task<OperationResult<InvoiceDto>> GetInvoiceAsync(Guid id) =>
        TraceAsync("Invoice.Get", () => inner.GetInvoiceAsync(id),
            a => a?.SetTag("invoice.id", id));

    public Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesByContactAsync(Guid contactId, int page, int pageSize) =>
        TraceAsync("Invoice.GetByContact", () => inner.GetInvoicesByContactAsync(contactId, page, pageSize),
            a => a?.SetTag("contact.id", contactId));

    public Task<OperationResult<InvoiceDto>> AddItemAsync(Guid invoiceId, CreateInvoiceItemRequest request) =>
        TraceAsync("Invoice.AddItem", () => inner.AddItemAsync(invoiceId, request),
            a => a?.SetTag("invoice.id", invoiceId));

    public Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request) =>
        TraceAsync("Invoice.RecordPayment", () => inner.RecordPaymentAsync(invoiceId, request),
            a => a?.SetTag("invoice.id", invoiceId));

    public Task<OperationResult<bool>> DeleteInvoiceAsync(Guid id) =>
        TraceAsync("Invoice.Delete", () => inner.DeleteInvoiceAsync(id),
            a => a?.SetTag("invoice.id", id));
}
