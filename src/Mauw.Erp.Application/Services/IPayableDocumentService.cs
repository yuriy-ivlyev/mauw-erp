using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface IPayableDocumentService
{
    Task<PagedResult<PayableDocumentListItemDto>> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? kind,
        string? status,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken);

    Task<PagedResult<PayableDocumentListItemDto>> ListMyReimbursementsAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PayableDocumentDto?> GetAsync(
        Guid id,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> CreateSupplierInvoiceAsync(
        CreatePayableDocumentRequest request,
        string? createdByUserId,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> CreateReimbursementAsync(
        CreatePayableDocumentRequest request,
        string? beneficiaryUserId,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> UpdateDraftAsync(
        Guid id,
        CreatePayableDocumentRequest request,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> UploadAttachmentAsync(
        Guid id,
        Guid version,
        Stream stream,
        string fileName,
        string contentType,
        long length,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken);

    Task<(Stream Stream, string ContentType, string FileName)?> DownloadAttachmentAsync(
        Guid id,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> SubmitAsync(
        Guid id,
        Guid version,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> ApproveAsync(
        Guid id,
        Guid version,
        string? approvedByUserId,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> RejectAsync(
        Guid id,
        Guid version,
        string? rejectedByUserId,
        string reason,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> CancelAsync(
        Guid id,
        Guid version,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken);

    Task<OperationResult<PayableDocumentDto>> RecordPaymentAsync(
        Guid id,
        PayableDocumentPaymentRequest request,
        string? recordedByUserId,
        CancellationToken cancellationToken);
}

public interface IPayableDocumentStorage
{
    Task<string> UploadAsync(Guid id, Stream stream, string contentType, CancellationToken cancellationToken);
    Task<(Stream Stream, string ContentType)> DownloadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
