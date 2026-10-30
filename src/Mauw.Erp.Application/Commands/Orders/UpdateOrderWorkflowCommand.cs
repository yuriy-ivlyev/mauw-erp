using System.Data;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Commands.Orders;

public enum OrderWorkflowAction
{
    StartPicking,
    PutOnHold,
    Resume,
    Complete,
    CancelRemainingLines
}

public sealed record UpdateOrderWorkflowCommand(
    Guid OrderId,
    OrderWorkflowAction Action,
    Guid? WarehouseOperatorId = null,
    string? Reason = null) : IRequest<OperationResult<OrderDto>>;

public sealed class UpdateOrderWorkflowCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateOrderWorkflowCommand, OperationResult<OrderDto>>
{
    public async Task<OperationResult<OrderDto>> Handle(
        UpdateOrderWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        var transactionStarted = false;
        try
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            transactionStarted = true;

            var order = await unitOfWork.Orders.GetByIdAsync(command.OrderId);
            if (order is null)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderDto>.Fail("Order not found");
            }

            switch (command.Action)
            {
                case OrderWorkflowAction.StartPicking:
                    order.StartPicking(command.WarehouseOperatorId
                        ?? throw new ArgumentException("A warehouse operator is required."));
                    break;
                case OrderWorkflowAction.PutOnHold:
                    order.PlaceOnHold(command.Reason
                        ?? throw new ArgumentException("A hold reason is required."));
                    break;
                case OrderWorkflowAction.Resume:
                    order.ReleaseHold();
                    break;
                case OrderWorkflowAction.Complete:
                    order.Complete();
                    break;
                case OrderWorkflowAction.CancelRemainingLines:
                    if (!order.FulfillmentWarehouseId.HasValue)
                        throw new InvalidOperationException("The order has no fulfillment warehouse.");
                    var ddts = await unitOfWork.DdtDocuments.GetByOrderIdWithItemsAsync(order.Id);
                    var remainingQuantities =
                        OrderInventoryOperations.GetRemainingShipmentQuantities(order, ddts);
                    await OrderInventoryOperations.ReleaseRemainingOrderReservationsAsync(
                        unitOfWork, order, order.FulfillmentWarehouseId.Value, remainingQuantities);
                    order.CancelRemainingLines(command.Reason
                        ?? throw new ArgumentException("A cancellation reason is required."));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command.Action));
            }

            await unitOfWork.Orders.UpdateAsync(order);
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
            return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(order));
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
    }
}
