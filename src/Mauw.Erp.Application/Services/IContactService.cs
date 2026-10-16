using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Contact application service – Party-Role pattern with soft delete.
/// </summary>
public interface IContactService
{
    Task<OperationResult<PagedResult<ContactSummaryDto>>> GetPagedAsync(ContactFilterRequest request);
    Task<OperationResult<ContactDetailDto>> GetByIdAsync(Guid id);
    Task<OperationResult<ContactDetailDto>> CreateAsync(CreateContactRequest request);
    Task<OperationResult<ContactDetailDto>> UpdateAsync(Guid id, UpdateContactRequest request);
    Task<OperationResult<bool>> DeleteAsync(Guid id);
    Task<OperationResult<bool>> ToggleBlockCustomerAsync(Guid contactId, bool isBlocked, string? reason);
}
