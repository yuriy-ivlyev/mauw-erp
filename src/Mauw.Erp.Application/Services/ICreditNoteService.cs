using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface ICreditNoteService
{
    Task<OperationResult<List<CreditNoteAvailableCreditDto>>> GetAvailableForContactAsync(
        Guid contactId,
        CancellationToken cancellationToken = default);
    Task<OperationResult<CreditNoteDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<OperationResult<CreditNotePreviewDto>> PreviewAsync(
        CreateCreditNoteRequest request,
        CancellationToken cancellationToken = default);
}
