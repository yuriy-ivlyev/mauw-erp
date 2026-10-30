using System.Data;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using MediatR;

namespace Mauw.Erp.Application.Commands.Orders;

public sealed record CreateShipmentCommand(
    Guid OrderId,
    string CarrierName,
    string? TrackingCode,
    IReadOnlyList<ShipOrderItemRequest>? Items = null,
    int? BoxCount = null,
    decimal? WeightKg = null) : IRequest<OperationResult<OrderFulfillmentResultDto>>;

public sealed class CreateShipmentCommandHandler(
    IUnitOfWork unitOfWork,
    ItalianAccountingCalculator accountingCalculator,
    IDdtService ddtService)
    : IRequestHandler<CreateShipmentCommand, OperationResult<OrderFulfillmentResultDto>>
{
    public async Task<OperationResult<OrderFulfillmentResultDto>> Handle(
        CreateShipmentCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.CarrierName) || command.CarrierName.Length > 200)
            return OperationResult<OrderFulfillmentResultDto>.Fail("Carrier name is required and must be at most 200 characters.");
        if (command.TrackingCode?.Length > 200)
            return OperationResult<OrderFulfillmentResultDto>.Fail("Tracking code must be at most 200 characters.");
        if (command.BoxCount is <= 0)
            return OperationResult<OrderFulfillmentResultDto>.Fail("At least one box is required.");
        if (command.WeightKg < 0m)
            return OperationResult<OrderFulfillmentResultDto>.Fail("Shipment weight cannot be negative.");

        Order order;
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
            if (order.Status == Domain.Enums.OrderStatus.Picking)
            {
                order.CompletePacking(command.BoxCount ?? 1, command.WeightKg ?? 0m);
            }
            else if (order.Status is not (Domain.Enums.OrderStatus.Packing or Domain.Enums.OrderStatus.PartiallyShipped))
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(
                    "Only orders in picking, packing, or partial shipment can be shipped.");
            }
            if (!order.FulfillmentWarehouseId.HasValue)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail("The order has no fulfillment warehouse.");
            }
            if (order.Items.Count == 0 || order.Items.Any(item => item.Quantity is 0 or int.MinValue))
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail("The order contains invalid item quantities.");
            }

            var previousDdts = (await unitOfWork.DdtDocuments.GetByOrderIdWithItemsAsync(order.Id)).ToList();
            var shippedQuantitiesBeforeShipment =
                OrderInventoryOperations.GetShippedQuantitiesByItem(order, previousDdts);
            var remainingQuantities = OrderInventoryOperations.GetRemainingShipmentQuantities(order, previousDdts);
            var shipmentQuantities = remainingQuantities.ToDictionary(item => item.Key, item => 0);
            if (command.Items is null)
            {
                foreach (var item in remainingQuantities)
                    shipmentQuantities[item.Key] = Math.Max(0, item.Value);
            }
            else
            {
                var requested = command.Items.ToList();
                if (requested.Select(item => item.OrderItemId).Distinct().Count() != requested.Count)
                {
                    await unitOfWork.RollbackAsync();
                    return OperationResult<OrderFulfillmentResultDto>.Fail(
                        "A shipment can include each order line only once.");
                }

                foreach (var item in requested)
                {
                    if (!remainingQuantities.TryGetValue(item.OrderItemId, out var remaining) ||
                        item.Quantity < 0 ||
                        item.Quantity > remaining)
                    {
                        await unitOfWork.RollbackAsync();
                        return OperationResult<OrderFulfillmentResultDto>.Fail(
                            "Shipment quantities must not exceed the remaining quantity of each order line.");
                    }

                    shipmentQuantities[item.OrderItemId] = item.Quantity;
                }
            }

            if (!shipmentQuantities.Values.Any(quantity => quantity > 0))
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(
                    "The shipment must contain at least one remaining order quantity.");
            }

            var allRemainingItemsShipped = remainingQuantities
                .Where(item => item.Value > 0)
                .All(item => shipmentQuantities[item.Key] == item.Value);

            var warehouseId = order.FulfillmentWarehouseId.Value;
            var warehouse = await unitOfWork.Warehouses.GetByIdAsync(warehouseId);
            if (warehouse is null || !warehouse.IsActive || !warehouse.IsSellable)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(
                    "Il magazzino dell'ordine non esiste, non è attivo o non è abilitato alla vendita.");
            }

            var shippedAtUtc = DateTime.UtcNow;
            long? sequence = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var candidate = await unitOfWork.DdtDocuments.GetNextShipmentSequenceAsync();
                var candidateDdtNumber = $"DDT-{shippedAtUtc:yyyy}-{candidate:D6}";
                if (await unitOfWork.DdtDocuments.GetByNumberAsync(candidateDdtNumber) is null)
                {
                    sequence = candidate;
                    break;
                }
            }
            if (!sequence.HasValue)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(
                    "Impossibile generare un numero DDT univoco.");
            }

            var ddtNumber = $"DDT-{shippedAtUtc:yyyy}-{sequence.Value:D6}";
            var shipmentNumber = $"SPED-{shippedAtUtc:yyyy}-{sequence.Value:D6}";
            var totalWeightKg = command.WeightKg ?? order.WeightKg;
            var shipment = new Shipment(
                order.Id,
                shipmentNumber,
                ddtNumber,
                command.CarrierName,
                command.TrackingCode,
                command.BoxCount ?? order.BoxCount ?? 1,
                totalWeightKg,
                shippedAtUtc);
            var ddt = new DdtDocument(order.ContactId, ddtNumber, warehouseId)
            {
                OrderId = order.Id,
                ShipmentId = shipment.Id,
                Shipment = shipment,
                DdtDate = shippedAtUtc
            };
            var stockError = await OrderInventoryOperations.PrepareShipmentAsync(
                unitOfWork, order, ddt, warehouseId, accountingCalculator, shipmentQuantities);
            if (stockError is not null)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(stockError);
            }

            foreach (var orderItem in order.Items)
            {
                var alreadyShipped = shippedQuantitiesBeforeShipment.GetValueOrDefault(orderItem.Id);
                orderItem.SetShippedQuantityFromHistory(alreadyShipped);
                var quantity = shipmentQuantities.GetValueOrDefault(orderItem.Id);
                if (quantity > 0)
                {
                    orderItem.RecordShipment(quantity);
                    shipment.AddLine(new ShipmentLine(
                        shipment.Id,
                        orderItem.Id,
                        orderItem.Variant.ProductId,
                        orderItem.Variant.Barcode,
                        quantity));
                }
            }

            order.AddShipment(shipment);
            order.Ship(command.CarrierName, command.TrackingCode, ddtNumber, allRemainingItemsShipped);
            await OrderInventoryOperations.RecordShipmentMovementsAsync(
                unitOfWork, order, ddt, warehouseId);
            await unitOfWork.DdtDocuments.AddAsync(ddt);
            await unitOfWork.Orders.UpdateAsync(order);
            await unitOfWork.SaveChangesAsync();

            cancellationToken.ThrowIfCancellationRequested();
            var ddtPdf = await ddtService.GetDdtPdfAsync(ddt.Id, cancellationToken);
            if (!ddtPdf.Success || ddtPdf.Data is null)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<OrderFulfillmentResultDto>.Fail(
                    ddtPdf.Error ?? "Impossibile generare il PDF del DDT.");
            }

            await unitOfWork.CommitAsync();

            return OperationResult<OrderFulfillmentResultDto>.Ok(new(
                OrderMapper.MapToDto(
                    order,
                    shippedQuantities: OrderInventoryOperations.GetShippedQuantitiesByItem(
                        order, previousDdts.Append(ddt))),
                $"ddt-{ddt.DdtNumber}.pdf",
                "application/pdf",
                ddtPdf.Data,
                new ShipmentDto(
                    shipment.Id,
                    shipment.ShipmentNumber,
                    shipment.DdtNumber,
                    shipment.CarrierName,
                    shipment.TrackingCode,
                    shipment.BoxCount,
                    shipment.TotalWeightKg,
                    shipment.ShippedAtUtc,
                    shipment.Lines.Select(line => new ShipmentLineDto(
                        line.OrderLineId,
                        line.ProductId,
                        line.Sku,
                        line.QuantityShipped)).ToList())));
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

        throw new InvalidOperationException("Shipment transaction ended without producing a DDT.");
    }
}
