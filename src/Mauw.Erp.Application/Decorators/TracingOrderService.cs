using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Decorators;

public sealed class TracingOrderService(IOrderService inner) : TracingDecoratorBase, IOrderService
{
    public Task<OperationResult<OrderDto>> CreateOrderAsync(CreateOrderRequest request) =>
        TraceAsync("Order.Create", () => inner.CreateOrderAsync(request),
            a => a?.SetTag("contact.id", request.ContactId));

    public Task<OperationResult<OrderDto>> GetOrderAsync(Guid id) =>
        TraceAsync("Order.Get", () => inner.GetOrderAsync(id),
            a => a?.SetTag("order.id", id));

    public Task<OperationResult<PagedResult<OrderDto>>> GetOrdersByContactAsync(Guid contactId, int page, int pageSize) =>
        TraceAsync("Order.GetByContact", () => inner.GetOrdersByContactAsync(contactId, page, pageSize),
            a => a?.SetTag("contact.id", contactId));

    public Task<OperationResult<OrderDto>> AddItemAsync(Guid orderId, CreateOrderItemRequest request) =>
        TraceAsync("Order.AddItem", () => inner.AddItemAsync(orderId, request),
            a =>
            {
                a?.SetTag("order.id", orderId);
                a?.SetTag("variant.id", request.VariantId);
            });

    public Task<OperationResult<OrderDto>> ConfirmOrderAsync(Guid orderId) =>
        TraceAsync("Order.Confirm", () => inner.ConfirmOrderAsync(orderId),
            a => a?.SetTag("order.id", orderId));

    public Task<OperationResult<bool>> DeleteOrderAsync(Guid id) =>
        TraceAsync("Order.Delete", () => inner.DeleteOrderAsync(id),
            a => a?.SetTag("order.id", id));
}