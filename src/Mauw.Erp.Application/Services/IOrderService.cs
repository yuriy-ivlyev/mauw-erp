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
    Task<OperationResult<PagedResult<OrderDto>>> GetAllOrdersAsync(string? search, string? status, string? orderType, int page, int pageSize);
    Task<OperationResult<PagedResult<OrderDto>>> GetOrdersByContactAsync(Guid contactId, int page, int pageSize);
    Task<OperationResult<OrderDto>> AddItemAsync(Guid orderId, CreateOrderItemRequest request);
    Task<OperationResult<OrderDto>> ConfirmOrderAsync(Guid orderId);
    Task<OperationResult<bool>> DeleteOrderAsync(Guid id);
    Task<OperationResult<byte[]>> GetOrderPdfAsync(Guid orderId);

    // Cassa session operations
    Task<OperationResult<CassaSessionDetailDto>> CreateCassaSessionAsync(CreateCassaSessionRequest request);
    Task<OperationResult<CassaSessionDetailDto>> UpdateCassaSessionAsync(Guid orderId, UpdateCassaSessionRequest request);
    Task<OperationResult<CassaSessionDetailDto>> ResumeCassaSessionAsync(Guid orderId, string? terminalId);
    Task<OperationResult<bool>> TerminateCassaSessionAsync(Guid orderId);
    Task<OperationResult<IEnumerable<CassaSessionSummaryDto>>> GetSuspendedSessionsAsync();
    Task<OperationResult<CassaSessionDetailDto>> GetCassaSessionAsync(Guid orderId);
}
