using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// DDT (Transport Document) application service - manages DDT operations.
/// </summary>
public interface IDdtService
{
    Task<OperationResult<DdtDto>> CreateDdtAsync(CreateDdtRequest request);
    Task<OperationResult<DdtDto>> GetDdtAsync(Guid id);
    Task<OperationResult<PagedResult<DdtDto>>> GetDdtsByContactAsync(Guid contactId, int page, int pageSize);
    Task<OperationResult<DdtDto>> AddItemAsync(Guid ddtId, CreateOrderItemRequest request);
    Task<OperationResult<bool>> DeleteDdtAsync(Guid id);
}