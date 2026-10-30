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

    public Task<OperationResult<PagedResult<OrderDto>>> GetAllOrdersAsync(
        string? search, string? status, string? orderType, int page, int pageSize, string? sortBy = null, bool sortDescending = false) =>
        inner.GetAllOrdersAsync(search, status, orderType, page, pageSize, sortBy, sortDescending);

    public Task<OperationResult<PagedResult<OrderDto>>> GetFulfillmentQueueAsync(int page, int pageSize) =>
        TraceAsync("Order.FulfillmentQueue", () => inner.GetFulfillmentQueueAsync(page, pageSize));

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

    public Task<OperationResult<OrderDto>> AddCompletedBillingItemAsync(
        Guid orderId, AddCompletedOrderItemRequest request) =>
        TraceAsync("Order.AddBillingItem", () => inner.AddCompletedBillingItemAsync(orderId, request),
            a =>
            {
                a?.SetTag("order.id", orderId);
                a?.SetTag("variant.id", request.VariantId);
            });

    public Task<OperationResult<OrderDto>> UpdateItemAsync(
        Guid orderId,
        Guid itemId,
        CreateOrderItemRequest request) =>
        TraceAsync("Order.UpdateItem", () => inner.UpdateItemAsync(orderId, itemId, request),
            a =>
            {
                a?.SetTag("order.id", orderId);
                a?.SetTag("order.item.id", itemId);
            });

    public Task<OperationResult<OrderDto>> RemoveDraftItemAsync(Guid orderId, Guid itemId) =>
        TraceAsync("Order.RemoveDraftItem", () => inner.RemoveDraftItemAsync(orderId, itemId),
            a =>
            {
                a?.SetTag("order.id", orderId);
                a?.SetTag("order.item.id", itemId);
            });

    public Task<OperationResult<OrderDto>> RemoveCompletedBillingItemAsync(Guid orderId, Guid itemId) =>
        TraceAsync("Order.RemoveBillingItem", () => inner.RemoveCompletedBillingItemAsync(orderId, itemId),
            a =>
            {
               a?.SetTag("order.id", orderId);
               a?.SetTag("order.item.id", itemId);
            });

    public Task<OperationResult<OrderDto>> UpdateShippingChargeAsync(
        Guid orderId,
        UpdateOrderShippingChargeRequest request) =>
        TraceAsync("Order.UpdateShippingCharge", () => inner.UpdateShippingChargeAsync(orderId, request),
            a => a?.SetTag("order.id", orderId));

    public Task<OperationResult<OrderDto>> UpdateCompletedInvoiceShippingChargeAsync(
        Guid orderId, UpdateOrderShippingChargeRequest request) =>
        TraceAsync("Order.UpdateInvoiceShippingCharge",
            () => inner.UpdateCompletedInvoiceShippingChargeAsync(orderId, request),
            a => a?.SetTag("order.id", orderId));

    public Task<OperationResult<OrderDto>> ConfirmOrderAsync(Guid orderId) =>
        TraceAsync("Order.Confirm", () => inner.ConfirmOrderAsync(orderId),
            a => a?.SetTag("order.id", orderId));

    public Task<OperationResult<bool>> DeleteOrderAsync(Guid id) =>
        TraceAsync("Order.Delete", () => inner.DeleteOrderAsync(id),
            a => a?.SetTag("order.id", id));

    public Task<OperationResult<byte[]>> GetOrderPdfAsync(Guid orderId) =>
        inner.GetOrderPdfAsync(orderId);

    public Task<OperationResult<CassaSessionDetailDto>> CreateCassaSessionAsync(CreateCassaSessionRequest request) =>
        inner.CreateCassaSessionAsync(request);

    public Task<OperationResult<CassaSessionDetailDto>> UpdateCassaSessionAsync(Guid orderId, UpdateCassaSessionRequest request) =>
        inner.UpdateCassaSessionAsync(orderId, request);

    public Task<OperationResult<CassaSessionDetailDto>> ResumeCassaSessionAsync(Guid orderId, string? terminalId) =>
        inner.ResumeCassaSessionAsync(orderId, terminalId);

    public Task<OperationResult<bool>> TerminateCassaSessionAsync(Guid orderId) =>
        inner.TerminateCassaSessionAsync(orderId);

    public Task<OperationResult<IEnumerable<CassaSessionSummaryDto>>> GetSuspendedSessionsAsync() =>
        inner.GetSuspendedSessionsAsync();

    public Task<OperationResult<CassaSessionDetailDto>> GetCassaSessionAsync(Guid orderId) =>
        inner.GetCassaSessionAsync(orderId);

    public OperationResult<CassaPreviewDto> PreviewCassaCart(CassaPreviewRequest request) =>
        inner.PreviewCassaCart(request);
}
