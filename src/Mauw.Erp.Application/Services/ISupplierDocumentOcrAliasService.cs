using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface ISupplierDocumentOcrAliasService
{
    Task<IReadOnlyList<SupplierDocumentOcrHeaderAliasDto>> ListAsync(CancellationToken cancellationToken);
    Task<OperationResult<SupplierDocumentOcrHeaderAliasDto>> AddAsync(
        CreateSupplierDocumentOcrHeaderAliasRequest request,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
