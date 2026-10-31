using System.Data;
using System.Globalization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using MediatR;

namespace Mauw.Erp.Application.Commands.Orders;

public sealed record SyncAndFinalizeInvoiceCommand(Guid OrderId) : IRequest<OperationResult<OrderDto>>;

/// <summary>
/// Overwrites the existing, not yet transmitted invoice with the adjusted order lines, recalculates the VAT
/// breakdown and totals, regenerates PDF and FatturaPA XML and closes the adjustment, all in one transaction.
/// The invoice keeps its identifier and number.
/// </summary>
public sealed class SyncAndFinalizeInvoiceCommandHandler(
    IUnitOfWork unitOfWork,
    IOrderService orderService,
    IInvoiceService invoiceService,
    ItalianAccountingCalculator accountingCalculator,
    ICurrentUserService currentUser)
    : IRequestHandler<SyncAndFinalizeInvoiceCommand, OperationResult<OrderDto>>
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public async Task<OperationResult<OrderDto>> Handle(
        SyncAndFinalizeInvoiceCommand command,
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
            var guard = OrderAdjustmentGuards.EnsureUnderAdjustment(order) ??
                OrderAdjustmentGuards.EnsureInvoiceAdjustable(invoice);
            if (guard is not null)
                return await FailAsync(guard);

            var invoiceId = invoice!.Id;
            var invoiceNumber = invoice.InvoiceNumber;
            var previous = (invoice.TaxableTotal, invoice.VatTotal, invoice.GrossTotal);

            var synchronized = invoice.SyncFromOrder(order, accountingCalculator);
            if (!synchronized.Success)
                return await FailAsync(synchronized.Error);

            var completed = order.CompleteAdjustment();
            if (!completed.Success)
                return await FailAsync(completed.Error!);

            await unitOfWork.AuditTrail.AddAsync(new DocumentAuditEntry(
                AuditDocumentTypes.Invoice,
                invoiceId,
                AuditActions.InvoiceSynchronizedFromOrder,
                $"Fattura {invoiceNumber} aggiornata dall'ordine {order.OrderNumber} (rettifica pre-SDI)",
                currentUser.UserId,
                currentUser.FullName ?? currentUser.Email,
                DescribeChange(previous, synchronized.Data!, invoice.Items.Count)));

            await unitOfWork.SaveChangesAsync();

            // Documents are derived from the persisted state; a failure here undoes the whole synchronization.
            var pdf = await invoiceService.GetInvoicePdfAsync(invoiceId);
            if (!pdf.Success || pdf.Data is not { Length: > 0 })
                return await FailAsync(pdf.Error ?? "Impossibile rigenerare il PDF della fattura.");
            var xml = await invoiceService.GetInvoiceXmlAsync(invoiceId);
            if (!xml.Success || string.IsNullOrWhiteSpace(xml.Data))
                return await FailAsync(xml.Error ?? "Impossibile rigenerare il tracciato XML della fattura.");

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

    private static string DescribeChange(
        (decimal Taxable, decimal Vat, decimal Gross) previous,
        CalculatedDocument current,
        int lineCount)
    {
        static string Money(decimal value) => value.ToString("0.00", Invariant);
        var breakdown = string.Join("; ", current.VatSummary.Select(group =>
            $"{group.VatRate.ToString("0.##", Invariant)}%{(group.NatureCode is null ? string.Empty : $" {group.NatureCode}")} " +
            $"imponibile {Money(group.TaxableAmount)} imposta {Money(group.VatAmount)}"));
        return $"righe {lineCount}; imponibile {Money(previous.Taxable)} -> {Money(current.TotalTaxable)}; " +
               $"IVA {Money(previous.Vat)} -> {Money(current.TotalVat)}; " +
               $"totale {Money(previous.Gross)} -> {Money(current.TotalGross)}; castelletto: {breakdown}";
    }
}
