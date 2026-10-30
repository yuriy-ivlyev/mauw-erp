using System.Data;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Commands.Orders;

public sealed record CancelOrderCommand(Guid OrderId, string Reason)
    : IRequest<OperationResult<OrderDto>>;

public sealed class CancelOrderCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CancelOrderCommand, OperationResult<OrderDto>>
{
    public async Task<OperationResult<OrderDto>> Handle(
        CancelOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            return OperationResult<OrderDto>.Fail("A cancellation reason is required.");

        Mauw.Erp.Domain.Entities.Order order;
        var transactionStarted = false;
        try
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            transactionStarted = true;
            var loadedOrder = await unitOfWork.Orders.GetByIdAsync(command.OrderId);
            if (loadedOrder is null)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderDto>.Fail("Order not found");
            }
            order = loadedOrder;
            if (order.Status is not (OrderStatus.Draft or OrderStatus.Confirmed or
                OrderStatus.PendingApproval or OrderStatus.Picking or OrderStatus.Packing or OrderStatus.OnHold))
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderDto>.Fail($"An order in status {order.Status} cannot be cancelled.");
            }

            var reservationStatus = order.Status == OrderStatus.OnHold
                ? order.StatusBeforeHold
                : order.Status;
            if (reservationStatus is (OrderStatus.Confirmed or OrderStatus.Picking or OrderStatus.Packing) &&
                order.FulfillmentWarehouseId.HasValue)
            {
                await OrderInventoryOperations.ReleaseOrderReservationsAsync(
                    unitOfWork, order, order.FulfillmentWarehouseId.Value);
            }

            order.Cancel(command.Reason);
            await unitOfWork.Orders.UpdateAsync(order);
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            if (transactionStarted)
                await unitOfWork.RollbackAsync();
            return OperationResult<OrderDto>.Fail(exception.Message);
        }
        catch
        {
            if (transactionStarted)
                await unitOfWork.RollbackAsync();
            throw;
        }

        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(order));
    }
}
