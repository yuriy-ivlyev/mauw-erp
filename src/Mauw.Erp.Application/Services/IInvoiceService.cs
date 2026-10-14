using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Invoice application service - manages invoice operations.
/// </summary>
public interface IInvoiceService
{
    Task<OperationResult<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request);
    Task<OperationResult<InvoiceDto>> GetInvoiceAsync(Guid id);
    Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesByContactAsync(Guid contactId, int page, int pageSize);
    Task<OperationResult<InvoiceDto>> AddItemAsync(Guid invoiceId, CreateInvoiceItemRequest request);
    Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request);
    Task<OperationResult<bool>> DeleteInvoiceAsync(Guid id);
}