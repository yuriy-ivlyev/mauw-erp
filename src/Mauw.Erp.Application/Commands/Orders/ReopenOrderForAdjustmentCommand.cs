using System.Data;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Commands.Orders;

public sealed record ReopenOrderForAdjustmentCommand(Guid OrderId) : IRequest<OperationResult<OrderDto>>;

/// <summary>
/// Temporarily reopens the lines of a completed order whose invoice has not been transmitted to the SDI.
/// </summary>
public sealed class ReopenOrderForAdjustmentCommandHandler(
    IUnitOfWork unitOfWork,
    IOrderService orderService,
    ICurrentUserService currentUser)
    : IRequestHandler<ReopenOrderForAdjustmentCommand, OperationResult<OrderDto>>
{
    public const string AuditNote = "Ordine riaperto per variazione pre-trasmissione SDI";

    public async Task<OperationResult<OrderDto>> Handle(
        ReopenOrderForAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        var transactionStarted = false;
        try
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            transactionStarted = true;
            await unitOfWork.Invoices.AcquireOrderInvoiceLockAsync(command.OrderId);

            var order = await unitOfWork.Orders.GetByIdAsync(command.OrderId);
            if (order is null)
                return await FailAsync("Order not found");

            var invoice = await unitOfWork.Invoices.GetByOrderIdAsync(command.OrderId);
            var guard = OrderAdjustmentGuards.EnsureInvoiceAdjustable(invoice);
            if (guard is not null)
                return await FailAsync(guard);

            var shippedError = await EnsureFullyShippedAsync(order);
            if (shippedError is not null)
                return await FailAsync(shippedError);

            var userId = currentUser.UserId;
            var reopened = order.BeginAdjustment(currentUser.FullName ?? currentUser.Email ?? userId);
            if (!reopened.Success)
                return await FailAsync(reopened.Error!);

            await unitOfWork.AuditTrail.AddAsync(new DocumentAuditEntry(
                AuditDocumentTypes.Order,
                order.Id,
                AuditActions.OrderReopenedForAdjustment,
                AuditNote,
                userId,
                currentUser.FullName ?? currentUser.Email,
                $"Fattura {invoice!.InvoiceNumber}; totale {invoice.GrossTotal:0.00}"));

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
            transactionStarted = false;
            return await orderService.GetOrderAsync(order.Id);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or DomainException or ConcurrencyException)
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

        async Task<OperationResult<OrderDto>> FailAsync(string error)
        {
            if (transactionStarted)
            {
                await unitOfWork.RollbackAsync();
                transactionStarted = false;
            }

            return OperationResult<OrderDto>.Fail(error);
        }
    }

    // Quantities are adjusted against what physically left the warehouse: a residual that was never shipped
    // (closed with "stralcio residuo") would make the stock delta ambiguous, so such orders are not reopened.
    private async Task<string?> EnsureFullyShippedAsync(Order order)
    {
        if (order.FulfillmentType != Domain.Enums.OrderFulfillmentType.StandardShipping)
            return null;

        var ddts = await unitOfWork.DdtDocuments.GetByOrderIdWithItemsAsync(order.Id);
        var shipped = OrderInventoryOperations.GetShippedQuantitiesByItem(order, ddts);
        return order.Items.Any(item =>
            item.AffectsStock && item.Quantity > 0 && shipped.GetValueOrDefault(item.Id) != item.Quantity)
            ? "L'ordine ha righe non interamente spedite: la rettifica pre-SDI non è disponibile."
            : null;
    }
}
