using System.Data;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Commands.Orders;

public sealed record ApproveOrderCommand(Guid OrderId)
    : IRequest<OperationResult<OrderDto>>;

public sealed class ApproveOrderCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<ApproveOrderCommand, OperationResult<OrderDto>>
{
    public async Task<OperationResult<OrderDto>> Handle(
        ApproveOrderCommand command,
        CancellationToken cancellationToken)
    {
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
            if (order.Status != OrderStatus.PendingApproval)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderDto>.Fail("Only an order pending approval can be approved.");
            }

            var warehouse = order.FulfillmentWarehouseId.HasValue
                ? await unitOfWork.Warehouses.GetByIdAsync(order.FulfillmentWarehouseId.Value)
                : await unitOfWork.Warehouses.GetDefaultWarehouseAsync();
            if (warehouse is null || !warehouse.IsActive || !warehouse.IsSellable)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderDto>.Fail(
                    "Non è disponibile un magazzino attivo e abilitato alla vendita.");
            }

            var reservationError = await OrderInventoryOperations.ReserveOrderAsync(
                unitOfWork, order, warehouse.Id);
            if (reservationError is not null)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderDto>.Fail(reservationError);
            }

            if (order.FulfillmentWarehouseId != warehouse.Id)
                order.SetFulfillmentWarehouse(warehouse.Id);
            order.Approve();
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
