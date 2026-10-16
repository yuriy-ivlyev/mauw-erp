using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface IContactAddressService
{
    Task<OperationResult<List<ContactAddressDto>>> GetAddressesAsync(Guid contactId);
    Task<OperationResult<ContactAddressDto>> AddAddressAsync(Guid contactId, ContactAddressRequest request);
    Task<OperationResult<ContactAddressDto>> UpdateAddressAsync(Guid contactId, Guid addressId, ContactAddressRequest request);
    Task<OperationResult<bool>> DeleteAddressAsync(Guid contactId, Guid addressId);
    Task<OperationResult<bool>> DeletePersonAsync(Guid contactId, Guid personId);
}
