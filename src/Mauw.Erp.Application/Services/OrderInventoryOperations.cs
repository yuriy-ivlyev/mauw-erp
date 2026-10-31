using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

internal static class OrderInventoryOperations
{
    public static async Task<string?> RecordCashSaleAsync(
        IUnitOfWork unitOfWork,
        Order order,
        Warehouse warehouse)
    {
        var validationError = await ValidateCashSaleAsync(unitOfWork, order, warehouse);
        if (validationError is not null)
            return validationError;

        await RecordCashSaleMovementsAsync(unitOfWork, order, warehouse);
        return null;
    }

    public static async Task<string?> ValidateCashSaleAsync(
        IUnitOfWork unitOfWork,
        Order order,
        Warehouse warehouse)
    {
        if (order.Items.Any(item => item.Quantity is 0 or int.MinValue))
            return "Una riga della sessione cassa ha una quantità non valida.";

        foreach (var stockDelta in order.Items
                     .GroupBy(item => item.VariantId)
                     .Select(group => new
                     {
                         VariantId = group.Key,
                         Delta = group.Sum(item => IsReturn(item)
                             ? Math.Abs((decimal)item.Quantity)
                             : -Math.Abs((decimal)item.Quantity))
                     })
                     .Where(item => item.Delta < 0)
                     .OrderBy(item => item.VariantId))
        {
            var available = await unitOfWork.Stock.GetAvailableQtyAsync(warehouse.Id, stockDelta.VariantId);
            if (available < -stockDelta.Delta)
                return $"Giacenza insufficiente per la variante {stockDelta.VariantId}: disponibili {available}.";
        }

        return null;
    }

    public static async Task RecordCashSaleMovementsAsync(
        IUnitOfWork unitOfWork,
        Order order,
        Warehouse warehouse)
    {
        foreach (var item in order.Items)
        {
            var movement = StockMovement.FromCassaItem(order, item, warehouse.Id);
            await unitOfWork.Stock.RecordMovementAsync(movement, movement.QuantityDelta!.Value);
        }
    }

    public static async Task<string?> ReserveOrderAsync(
        IUnitOfWork unitOfWork,
        Order order,
        Guid warehouseId)
    {
        foreach (var requirement in GetSaleQuantities(order)
                     .OrderBy(requirement => requirement.VariantId))
        {
            var stock = await unitOfWork.Stock.GetOrCreateStockItemAsync(
                warehouseId, requirement.VariantId);
            if (stock.QuantityAvailable < requirement.Quantity)
                return $"Giacenza insufficiente per la variante {requirement.VariantId}: " +
                       $"disponibili {stock.QuantityAvailable}.";
        }

        foreach (var requirement in GetSaleQuantities(order)
                     .OrderBy(requirement => requirement.VariantId))
        {
            var stock = await unitOfWork.Stock.GetOrCreateStockItemAsync(
                warehouseId, requirement.VariantId);
            stock.Reserve(requirement.Quantity);
        }

        return null;
    }

    public static async Task ReleaseOrderReservationsAsync(
        IUnitOfWork unitOfWork,
        Order order,
        Guid warehouseId)
    {
        foreach (var requirement in GetSaleQuantities(order)
                     .OrderBy(requirement => requirement.VariantId))
        {
            var stock = await unitOfWork.Stock.GetStockItemAsync(warehouseId, requirement.VariantId);
            stock?.ReleaseReservation(requirement.Quantity);
        }
    }

    public static async Task ReleaseRemainingOrderReservationsAsync(
        IUnitOfWork unitOfWork,
        Order order,
        Guid warehouseId,
        IReadOnlyDictionary<Guid, int> remainingQuantities)
    {
        foreach (var requirement in order.Items
                     .Where(item => remainingQuantities.GetValueOrDefault(item.Id) > 0)
                     .GroupBy(item => item.VariantId)
                     .Select(group => new
                     {
                         VariantId = group.Key,
                         Quantity = group.Sum(item => remainingQuantities.GetValueOrDefault(item.Id))
                     })
                     .OrderBy(item => item.VariantId))
        {
            var stock = await unitOfWork.Stock.GetStockItemAsync(warehouseId, requirement.VariantId);
            stock?.ReleaseReservation(requirement.Quantity);
        }
    }

    public static async Task<string?> PrepareShipmentAsync(
        IUnitOfWork unitOfWork,
        Order order,
        DdtDocument ddt,
        Guid warehouseId,
        ItalianAccountingCalculator accountingCalculator,
        IReadOnlyDictionary<Guid, int> shipmentQuantities)
    {
        var saleRequirements = order.Items
            .Select(item => new
            {
                item.VariantId,
                Quantity = Math.Max(0, shipmentQuantities.GetValueOrDefault(item.Id))
            })
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.VariantId)
            .Select(group => (VariantId: group.Key, Quantity: (decimal)group.Sum(item => item.Quantity)))
            .ToList();

        foreach (var requirement in saleRequirements
                     .OrderBy(requirement => requirement.VariantId))
        {
            var stock = await unitOfWork.Stock.GetStockItemAsync(warehouseId, requirement.VariantId);
            if (stock is null || stock.QuantityOnHand < requirement.Quantity)
                return $"Giacenza fisica insufficiente per la variante {requirement.VariantId}.";
            if (stock.QuantityReserved < requirement.Quantity)
                return $"Riserva non disponibile per la variante {requirement.VariantId}.";
        }

        foreach (var orderItem in order.Items)
        {
            var quantity = shipmentQuantities.GetValueOrDefault(orderItem.Id);
            if (quantity == 0)
                continue;

            var ddtItem = new DdtItem(
                ddt.Id,
                orderItem.VariantId,
                quantity,
                orderItem.UnitNetPrice,
                orderItem.VatRate,
                orderItem.NatureCode,
                orderItem.DiscountPercent)
            {
                PricingSnapshot = orderItem.PricingSnapshot
            };
            ddt.Items.Add(ddtItem);
        }

        var accounting = ddt.UpdateTotals(accountingCalculator);
        return accounting.Success ? null : accounting.Error;
    }

    public static Dictionary<Guid, int> GetShippedQuantitiesByItem(
        Order order,
        IEnumerable<DdtDocument> ddts)
    {
        var orderItems = order.Items.ToList();
        if (orderItems.Any(item => item.QuantityShipped > 0))
        {
            return orderItems.ToDictionary(
                item => item.Id,
                item => Math.Min(Math.Max(item.Quantity, 0), item.QuantityShipped));
        }

        var shippedByVariant = ddts
            .SelectMany(ddt => ddt.Items)
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.VariantId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var shippedByItem = new Dictionary<Guid, int>();

        foreach (var group in order.Items.GroupBy(item => item.VariantId))
        {
            var remainingShipped = shippedByVariant.GetValueOrDefault(group.Key);
            foreach (var item in group)
            {
                var shipped = Math.Min(Math.Max(item.Quantity, 0), remainingShipped);
                shippedByItem[item.Id] = shipped;
                remainingShipped -= shipped;
            }
        }

        return shippedByItem;
    }

    public static Dictionary<Guid, int> GetRemainingShipmentQuantities(
        Order order,
        IEnumerable<DdtDocument> ddts)
    {
        var shippedByItem = GetShippedQuantitiesByItem(order, ddts);
        var shippedReturnsByVariant = ddts
            .SelectMany(ddt => ddt.Items)
            .Where(item => item.Quantity < 0)
            .GroupBy(item => item.VariantId)
            .ToDictionary(group => group.Key, group => group.Sum(item => Math.Abs(item.Quantity)));

        var remaining = new Dictionary<Guid, int>();
        foreach (var group in order.Items.GroupBy(item => item.VariantId))
        {
            var returnAlreadyShipped = shippedReturnsByVariant.GetValueOrDefault(group.Key) > 0;
            foreach (var item in group)
            {
                if (item.Quantity > 0)
                    remaining[item.Id] = Math.Max(0, item.Quantity - shippedByItem.GetValueOrDefault(item.Id));
                else
                    remaining[item.Id] = returnAlreadyShipped ? 0 : item.Quantity;
            }
        }

        return remaining;
    }

    public static async Task RecordShipmentMovementsAsync(
        IUnitOfWork unitOfWork,
        Order order,
        DdtDocument ddt,
        Guid warehouseId)
    {
        foreach (var ddtItem in ddt.Items)
        {
            var movement = StockMovement.FromDdtItem(ddt, ddtItem, warehouseId);
            if (movement.QuantityDelta < 0)
            {
                var stock = await unitOfWork.Stock.GetStockItemAsync(warehouseId, ddtItem.VariantId)
                    ?? throw new InvalidOperationException(
                        $"Stock item for variant {ddtItem.VariantId} disappeared during shipment.");
                stock.ReleaseReservation(-movement.QuantityDelta.Value);
            }

            await unitOfWork.Stock.RecordMovementAsync(movement, movement.QuantityDelta!.Value);
        }
    }

    public static async Task<(Guid? WarehouseId, string? Error)> ResolveAdjustmentWarehouseAsync(
        IUnitOfWork unitOfWork,
        Order order)
    {
        if (order.FulfillmentWarehouseId.HasValue)
            return (order.FulfillmentWarehouseId, null);

        var warehouse = await unitOfWork.Warehouses.GetDefaultWarehouseAsync();
        return warehouse is { IsActive: true, IsSellable: true }
            ? (warehouse.Id, null)
            : (null, "Non è disponibile un magazzino predefinito attivo e abilitato alla vendita.");
    }

    /// <summary>
    /// Posts the stock delta of an order adjustment: an integrative discharge when quantities grow,
    /// a return when they shrink. Nothing is recorded if any discharge exceeds the available stock.
    /// </summary>
    public static async Task<string?> RecordAdjustmentMovementsAsync(
        IUnitOfWork unitOfWork,
        Order order,
        Guid warehouseId,
        IReadOnlyList<OrderAdjustmentStockDelta> deltas,
        string? createdByUserId,
        string notes)
    {
        foreach (var delta in deltas.Where(delta => delta.DeltaOnHand < 0))
        {
            var available = await unitOfWork.Stock.GetAvailableQtyAsync(warehouseId, delta.VariantId);
            if (available < -delta.DeltaOnHand)
                return $"Giacenza insufficiente per la variante {delta.VariantId}: disponibili {available}.";
        }

        foreach (var delta in deltas)
        {
            var movement = StockMovement.FromOrderAdjustment(
                order, delta.VariantId, delta.DeltaOnHand, warehouseId, createdByUserId, notes);
            await unitOfWork.Stock.RecordMovementAsync(movement, delta.DeltaOnHand);
        }

        return null;
    }

    private static IEnumerable<(Guid VariantId, decimal Quantity)> GetSaleQuantities(Order order) =>
        order.Items
            .Where(item => item.Quantity > 0 && !IsReturn(item))
            .GroupBy(item => item.VariantId)
            .Select(group => (group.Key, group.Sum(item => (decimal)item.Quantity)));

    private static bool IsReturn(OrderItem item) =>
        item.PricingSnapshot?.IsCustomerReturn == true || item.Quantity < 0;
}
