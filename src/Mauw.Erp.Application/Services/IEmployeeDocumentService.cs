using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface IEmployeeDocumentService
{
    Task<PagedResult<EmployeeDocumentDto>> ListAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<EmployeeDocumentDto>> CreateAsync(
        Guid employeeId,
        SaveEmployeeDocumentRequest request,
        Stream attachment,
        string fileName,
        string contentType,
        long length,
        string? userId,
        CancellationToken cancellationToken);

    Task<OperationResult<EmployeeDocumentDto>> UpdateAsync(
        Guid employeeId,
        Guid documentId,
        SaveEmployeeDocumentRequest request,
        string? userId,
        CancellationToken cancellationToken);

    Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(
        Guid employeeId,
        Guid documentId,
        CancellationToken cancellationToken);
}

public interface IEmployeeDocumentStorage
{
    Task<string> UploadAsync(
        Guid employeeId,
        Guid documentId,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken);

    Task<(Stream Stream, string ContentType)> DownloadAsync(
        string key,
        CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
