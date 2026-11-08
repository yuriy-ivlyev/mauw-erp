using Mauw.Erp.Contracts.Dtos;

namespace Mauw.Erp.Application.Services;

public interface ISupplierDocumentOcrProvider
{
    Task<OperationResult<ParsedDocumentResponse>> ParseAsync(
        Stream document,
        CancellationToken cancellationToken = default);
}
