using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Invoice application service - manages invoice operations.
/// </summary>
public interface IInvoiceService
{
    Task<OperationResult<InvoiceDto>> GetInvoiceAsync(Guid id);
    Task<OperationResult<InvoiceDto>> GenerateFromOrderAsync(Guid orderId);
    Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesAsync(
        Guid? contactId,
        string? search,
        int page,
        int pageSize,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        bool unpaidOnly = false,
        string? sortBy = null,
        bool sortDescending = true);
    Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesByContactAsync(Guid contactId, int page, int pageSize);
    Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request);
    Task<OperationResult<byte[]>> GetInvoicePdfAsync(Guid id);

    /// <summary>FatturaPA (FPR12) XML regenerated from the persisted invoice.</summary>
    Task<OperationResult<string>> GetInvoiceXmlAsync(Guid id);
}