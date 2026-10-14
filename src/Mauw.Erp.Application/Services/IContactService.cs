using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Contact application service.
/// </summary>
public interface IContactService
{
    Task<OperationResult<ContactDto>> CreateContactAsync(CreateContactRequest request);
    Task<OperationResult<ContactDto>> GetContactAsync(Guid id);
    Task<OperationResult<PagedResult<ContactDto>>> GetContactsAsync(int page, int pageSize, string? search = null);
    Task<OperationResult<ContactDto>> UpdateContactAsync(Guid id, UpdateContactRequest request);
    Task<OperationResult<bool>> DeleteContactAsync(Guid id);
}