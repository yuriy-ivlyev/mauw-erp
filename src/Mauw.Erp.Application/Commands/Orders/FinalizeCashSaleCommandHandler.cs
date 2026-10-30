using System.Data;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Commands.Orders;

public sealed record FinalizeCashSaleCommand(
    Guid OrderId,
    string PaymentMethod,
    string? TransactionDetails) : IRequest<OperationResult<OrderFulfillmentResultDto>>;

public sealed class FinalizeCashSaleCommandHandler(
    IUnitOfWork unitOfWork,
    IOrderService orderService)
    : IRequestHandler<FinalizeCashSaleCommand, OperationResult<OrderFulfillmentResultDto>>
{
    public async Task<OperationResult<OrderFulfillmentResultDto>> Handle(
        FinalizeCashSaleCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.PaymentMethod) || command.PaymentMethod.Length > 50)
            return OperationResult<OrderFulfillmentResultDto>.Fail(
                "Payment method is required and must be at most 50 characters.");
        if (command.TransactionDetails?.Length > 500)
            return OperationResult<OrderFulfillmentResultDto>.Fail(
                "Transaction details must be at most 500 characters.");

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
                return OperationResult<OrderFulfillmentResultDto>.Fail("Order not found");
            }
            order = loadedOrder;
            if (order.FulfillmentType != OrderFulfillmentType.CashDesk)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail("Only cash-desk orders can be finalized as a cash sale.");
            }
            if (order.Items.Count == 0)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail("Cannot finalize a cash sale without items.");
            }
            if (order.Status == Domain.Enums.OrderStatus.Suspended)
                order.Resume(order.TerminalId);

            var warehouse = await unitOfWork.Warehouses.GetDefaultWarehouseAsync();
            if (warehouse is null || !warehouse.IsActive || !warehouse.IsSellable)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(
                    "Non è disponibile un magazzino predefinito attivo e abilitato alla vendita.");
            }

            var stockError = await OrderInventoryOperations.ValidateCashSaleAsync(unitOfWork, order, warehouse);
            if (stockError is not null)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(stockError);
            }

            order.FinalizeCashSale(command.PaymentMethod, command.TransactionDetails);
            await OrderInventoryOperations.RecordCashSaleMovementsAsync(unitOfWork, order, warehouse);
            await unitOfWork.Orders.UpdateAsync(order);
            await unitOfWork.SaveChangesAsync();

            var receipt = await orderService.GetOrderPdfAsync(order.Id);
            if (!receipt.Success || receipt.Data is null)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(
                    receipt.Error ?? "Impossibile generare la ricevuta della vendita.");
            }

            await unitOfWork.CommitAsync();

            return OperationResult<OrderFulfillmentResultDto>.Ok(new(
                OrderMapper.MapToDto(order),
                $"ricevuta-{order.OrderNumber}.pdf",
                "application/pdf",
                receipt.Data));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            if (transactionStarted)
                await unitOfWork.RollbackAsync();
            return OperationResult<OrderFulfillmentResultDto>.Fail(exception.Message);
        }
        catch
        {
            if (transactionStarted)
                await unitOfWork.RollbackAsync();
            throw;
        }

        throw new InvalidOperationException("Cash sale transaction ended without producing a receipt.");
    }
}
