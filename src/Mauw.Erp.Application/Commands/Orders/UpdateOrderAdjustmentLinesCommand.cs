using System.Data;
using System.Globalization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using MediatR;

namespace Mauw.Erp.Application.Commands.Orders;

/// <summary>One line of the desired state: an existing line (<see cref="OrderItemId"/>) or a new variant.</summary>
public sealed record OrderAdjustmentLine(Guid? OrderItemId, Guid? VariantId, int Quantity, string? NatureCode = null);

public sealed record UpdateOrderAdjustmentLinesCommand(
    Guid OrderId,
    IReadOnlyList<OrderAdjustmentLine> Lines) : IRequest<OperationResult<OrderDto>>;

/// <summary>
/// Adds, removes or re-quantifies the lines of an order reopened for adjustment and posts the stock delta
/// (further discharge or return) in the same transaction.
/// </summary>
public sealed class UpdateOrderAdjustmentLinesCommandHandler(
    IUnitOfWork unitOfWork,
    IOrderService orderService,
    IDocumentPricingService documentPricing,
    ItalianAccountingCalculator accountingCalculator,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateOrderAdjustmentLinesCommand, OperationResult<OrderDto>>
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public async Task<OperationResult<OrderDto>> Handle(
        UpdateOrderAdjustmentLinesCommand command,
        CancellationToken cancellationToken)
    {
        var requestError = ValidateRequest(command);
        if (requestError is not null)
            return OperationResult<OrderDto>.Fail(requestError);

        var transactionStarted = false;
        try
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            transactionStarted = true;
            await unitOfWork.Invoices.AcquireOrderInvoiceLockAsync(command.OrderId);

            var order = await unitOfWork.Orders.GetByIdAsync(command.OrderId);
            if (order is null)
                return await FailAsync("Order not found");

            var guard = OrderAdjustmentGuards.EnsureUnderAdjustment(order) ??
                OrderAdjustmentGuards.EnsureInvoiceAdjustable(
                    await unitOfWork.Invoices.GetByOrderIdAsync(command.OrderId));
            if (guard is not null)
                return await FailAsync(guard);

            var keptLines = command.Lines
                .Where(line => line.OrderItemId.HasValue)
                .Select(line => new OrderLineQuantityChange(line.OrderItemId!.Value, line.Quantity))
                .ToList();
            var addedItems = new List<OrderItem>();
            foreach (var line in command.Lines.Where(line => !line.OrderItemId.HasValue))
            {
                var created = await CreateItemAsync(order, line);
                if (!created.Success)
                    return await FailAsync(created.Error!);
                addedItems.Add(created.Data!);
            }

            var before = OrderAdjustmentStockCalculator.GetStockEffects(order.Items);
            var previousTotal = order.GrossTotal;
            var summary = Summarize(order, keptLines, addedItems.Count);

            var adjusted = order.AdjustLines(keptLines, addedItems, accountingCalculator);
            if (!adjusted.Success)
                return await FailAsync(adjusted.Error);

            var deltas = OrderAdjustmentStockCalculator.CalculateDeltas(
                before, OrderAdjustmentStockCalculator.GetStockEffects(order.Items));
            if (deltas.Count > 0)
            {
                var warehouse = await OrderInventoryOperations.ResolveAdjustmentWarehouseAsync(unitOfWork, order);
                if (warehouse.WarehouseId is null)
                    return await FailAsync(warehouse.Error!);

                var stockError = await OrderInventoryOperations.RecordAdjustmentMovementsAsync(
                    unitOfWork, order, warehouse.WarehouseId.Value, deltas, currentUser.UserId,
                    $"Rettifica pre-SDI ordine {order.OrderNumber}");
                if (stockError is not null)
                    return await FailAsync(stockError);
            }

            await unitOfWork.AuditTrail.AddAsync(new DocumentAuditEntry(
                AuditDocumentTypes.Order,
                order.Id,
                AuditActions.OrderLinesAdjusted,
                "Righe ordine rettificate prima della trasmissione SDI",
                currentUser.UserId,
                currentUser.FullName ?? currentUser.Email,
                $"{summary}; totale {previousTotal.ToString("0.00", Invariant)} -> {order.GrossTotal.ToString("0.00", Invariant)}; " +
                $"delta magazzino: {DescribeDeltas(deltas)}"));

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

    private static string? ValidateRequest(UpdateOrderAdjustmentLinesCommand command)
    {
        if (command.OrderId == Guid.Empty)
            return "Ordine non valido.";
        if (command.Lines is null || command.Lines.Any(line => line is null))
            return "Indicare le righe della rettifica.";
        foreach (var line in command.Lines)
        {
            if (line.Quantity <= 0)
                return "La quantità di ogni riga deve essere un intero maggiore di zero.";
            if (!line.OrderItemId.HasValue && (!line.VariantId.HasValue || line.VariantId == Guid.Empty))
                return "Una nuova riga richiede la variante.";
            if (line.OrderItemId == Guid.Empty)
                return "Riga ordine non valida.";
        }

        return null;
    }

    private async Task<OperationResult<OrderItem>> CreateItemAsync(Order order, OrderAdjustmentLine line)
    {
        var variant = await unitOfWork.ProductVariants.GetByIdAsync(line.VariantId!.Value);
        if (variant is null)
            return OperationResult<OrderItem>.Fail("Variante non trovata.");
        var product = await unitOfWork.Products.GetByIdAsync(variant.ProductId);
        if (product is null)
            return OperationResult<OrderItem>.Fail("Prodotto non trovato.");

        var pricing = await documentPricing.PrepareAsync(order.ContactId,
            [new(variant.Id, line.Quantity, null, null, product.VatRate, line.NatureCode, null,
                DefaultDiscountPercent: order.CustomerDiscountRate)],
            DateTimeOffset.UtcNow);
        if (!pricing.Success)
            return OperationResult<OrderItem>.Fail(pricing.Error ?? "Impossibile calcolare il prezzo della riga.");

        var prepared = pricing.Data!.Lines[0];
        return OperationResult<OrderItem>.Ok(new OrderItem(
            order.Id,
            variant.Id,
            line.Quantity,
            prepared.FiscalLine.UnitNetPrice,
            prepared.FiscalLine.VatRate,
            natureCode: prepared.FiscalLine.NatureCode,
            discountPercent: prepared.FiscalLine.DiscountPercent)
        {
            PricingSnapshot = prepared.Snapshot,
            Variant = variant
        });
    }

    private static string Summarize(Order order, IReadOnlyCollection<OrderLineQuantityChange> kept, int added)
    {
        var current = order.Items.Where(item => !item.IsExcludedFromInvoice).ToList();
        var keptQuantities = kept.ToDictionary(line => line.OrderItemId, line => line.Quantity);
        var removed = current.Count(item => !keptQuantities.ContainsKey(item.Id));
        var modified = current.Count(item =>
            keptQuantities.TryGetValue(item.Id, out var quantity) && quantity != item.Quantity);
        return $"righe aggiunte {added}, rimosse {removed}, quantità modificate {modified}";
    }

    private string DescribeDeltas(IReadOnlyList<OrderAdjustmentStockDelta> deltas) =>
        deltas.Count == 0
            ? "nessuno"
            : string.Join(", ", deltas.Select(delta =>
                $"{delta.VariantId:N}:{delta.DeltaOnHand.ToString("+0;-0", Invariant)}"));
}
