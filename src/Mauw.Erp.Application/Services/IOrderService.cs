using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Order application service - manages order operations.
/// </summary>
public interface IOrderService
{
    Task<OperationResult<OrderDto>> CreateOrderAsync(CreateOrderRequest request);
    Task<OperationResult<OrderDto>> GetOrderAsync(Guid id);
    Task<OperationResult<PagedResult<OrderDto>>> GetOrdersByContactAsync(Guid contactId, int page, int pageSize);
    Task<OperationResult<OrderDto>> AddItemAsync(Guid orderId, CreateOrderItemRequest request);
    Task<OperationResult<OrderDto>> ConfirmOrderAsync(Guid orderId);
    Task<OperationResult<bool>> DeleteOrderAsync(Guid id);
}