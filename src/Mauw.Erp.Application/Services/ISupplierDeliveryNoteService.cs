using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface ISupplierDeliveryNoteService
{
    Task<PagedResult<SupplierDeliveryNoteListItemDto>> ListAsync(
        int page, int pageSize, string? search, Guid? supplierId, string? status,
        DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct);
    Task<SupplierDeliveryNoteDto?> GetAsync(Guid id, CancellationToken ct);
    Task<OperationResult<SupplierArticleMatchDto>> FindArticleAsync(Guid id, string supplierItemCode, CancellationToken ct);
    Task<OperationResult<SupplierDeliveryNoteDto>> UploadAsync(
        UploadSupplierDeliveryNoteRequest request, Stream stream, string fileName, string contentType, long length, CancellationToken ct);
    Task<OperationResult<SupplierDeliveryNoteDto>> SaveLinesAsync(
        Guid id, SaveSupplierDeliveryNoteLinesRequest request, bool parsed, CancellationToken ct);
    Task<OperationResult<SupplierDeliveryNoteDto>> ConfirmAsync(Guid id, Guid version, CancellationToken ct);
    Task<OperationResult<SupplierDeliveryNoteDto>> CancelAsync(Guid id, Guid version, CancellationToken ct);
    Task<(Stream Stream, string ContentType)?> DownloadAsync(Guid id, CancellationToken ct);
}

public interface ISupplierDocumentStorage
{
    Task<string> UploadAsync(Guid id, Stream stream, string contentType, CancellationToken ct);
    Task<(Stream Stream, string ContentType)> DownloadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
