using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Contracts.Enums;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class InventoryStockQueryPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    public static IEnumerable<object?[]> ValuationCases =>
    [
        [ValuationMethod.WeightedAverageCost, (DateTime?)null, 5m, 16.12345678m, 80.62m, "StockMovements"],
        [ValuationMethod.LastPurchaseCost, (DateTime?)null, 5m, 20.12345678m, 100.62m, "StockMovements"],
        [ValuationMethod.WeightedAverageCost, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), 2m, 10.12345678m, 20.25m, "StockMovements"],
        [ValuationMethod.WeightedAverageCost, new DateTime(2025, 12, 31, 10, 0, 0, DateTimeKind.Utc), 0m, 9m, 0m, "PurchaseCost"]
    ];

    [Theory]
    [MemberData(nameof(ValuationCases))]
    public async Task GetStocksAsync_UsesSelectedCostAndCutoff(
        ValuationMethod valuationMethod,
        DateTime? atDate,
        decimal expectedQuantity,
        decimal expectedUnitCost,
        decimal expectedTotalValue,
        string expectedCostSource)
    {
        var database = postgres.CreateDatabase();
        Guid warehouseId;
        DateTime firstReceiptDate;

        await using (var seed = database.CreateDbContext())
        {
            var supplier = new Contact("Fornitore valorizzazione test");
            supplier.EnableAsSupplier();
            seed.Add(supplier);
            await seed.SaveChangesAsync();

            var address = new ContactAddress(
                supplier.Id,
                AddressPurpose.LegalHeadquarters,
                "Via Test 1",
                "20100",
                "Milano");
            seed.Add(address);
            await seed.SaveChangesAsync();

            var code = Guid.NewGuid().ToString("N");
            var warehouse = new Warehouse(
                $"VAL-{code}"[..12],
                "Deposito valorizzazione test",
                WarehouseType.Standard,
                address.Id,
                isSellable: true,
                isDefault: true);
            var product = new Product(supplier.Id, code, code, "Articolo valorizzazione", 22m, 9m);
            seed.AddRange(warehouse, product);
            await seed.SaveChangesAsync();

            var variant = new ProductVariant(product.Id, $"BAR-{code}", 9m, supplier.Id);
            seed.Add(variant);
            await seed.SaveChangesAsync();

            var stock = new StockItem(warehouse.Id, variant.Id, unitCost: 77m);
            stock.ApplyMovement(5m);
            var firstReceipt = new StockMovement(
                variant.Id,
                warehouse.Id,
                StockMovementReason.SupplierReceipt,
                2m);
            var secondReceipt = new StockMovement(
                variant.Id,
                warehouse.Id,
                StockMovementReason.SupplierReceipt,
                3m);
            seed.AddRange(stock, firstReceipt, secondReceipt);
            await seed.SaveChangesAsync();

            firstReceiptDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_movements SET unit_cost = {10.12345678m}, movement_date_utc = {firstReceiptDate} WHERE id = {firstReceipt.Id}");
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_movements SET unit_cost = {20.12345678m}, movement_date_utc = {firstReceiptDate.AddDays(1)} WHERE id = {secondReceipt.Id}");
            warehouseId = warehouse.Id;
        }

        await using var db = database.CreateDbContext();
        var service = new InventoryStockQueryService(db);
        var result = await service.GetStocksAsync(new InventoryStockFilterRequest
        {
            WarehouseId = warehouseId,
            AtDate = atDate,
            ValuationMethod = valuationMethod,
            Page = 1,
            PageSize = 20
        });

        Assert.True(result.Success, result.Error);
        var row = Assert.Single(result.Data!.Items);
        Assert.Equal(expectedQuantity, row.QuantityOnHand);
        Assert.Equal(expectedUnitCost, row.UnitCost);
        Assert.Equal(expectedTotalValue, row.TotalValue);
        Assert.Equal(expectedCostSource, row.CostSource);
        Assert.Equal(expectedTotalValue, result.Data.Totals.TotalValue);
    }

    [Fact]
    public async Task GetMovementsAsync_LocalizesReasonsAndKeepsBalancesAcrossPages()
    {
        var database = postgres.CreateDatabase();
        Guid warehouseId;
        Guid variantId;
        Guid firstId;
        Guid secondId;
        Guid thirdId;
        var firstDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var secondDate = firstDate.AddDays(1);
        var thirdDate = firstDate.AddDays(2);

        await using (var seed = database.CreateDbContext())
        {
            var supplier = new Contact("Fornitore mastrino test");
            supplier.EnableAsSupplier();
            seed.Add(supplier);
            await seed.SaveChangesAsync();

            var address = new ContactAddress(
                supplier.Id,
                AddressPurpose.LegalHeadquarters,
                "Via Test 1",
                "20100",
                "Milano");
            seed.Add(address);
            await seed.SaveChangesAsync();

            var code = Guid.NewGuid().ToString("N");
            var warehouse = new Warehouse(
                $"MOV-{code}"[..12],
                "Deposito mastrino test",
                WarehouseType.Standard,
                address.Id,
                isSellable: true,
                isDefault: true);
            var product = new Product(supplier.Id, code, code, "Articolo mastrino", 22m, 9m);
            seed.AddRange(warehouse, product);
            await seed.SaveChangesAsync();

            var variant = new ProductVariant(product.Id, $"BAR-{code}", 9m, supplier.Id);
            seed.Add(variant);
            await seed.SaveChangesAsync();

            var first = new StockMovement(variant.Id, warehouse.Id, StockMovementReason.SupplierReceipt, 10m);
            var second = new StockMovement(variant.Id, warehouse.Id, StockMovementReason.InventoryAdjustment, -3m);
            var third = new StockMovement(variant.Id, warehouse.Id, StockMovementReason.CustomerReturn, 2m);
            seed.AddRange(first, second, third);
            await seed.SaveChangesAsync();
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_movements SET movement_date_utc = {firstDate} WHERE id = {first.Id}");
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_movements SET movement_date_utc = {secondDate} WHERE id = {second.Id}");
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_movements SET movement_date_utc = {thirdDate} WHERE id = {third.Id}");

            warehouseId = warehouse.Id;
            variantId = variant.Id;
            firstId = first.Id;
            secondId = second.Id;
            thirdId = third.Id;
        }

        await using var db = database.CreateDbContext();
        var service = new InventoryStockQueryService(db);
        var firstPage = await service.GetMovementsAsync(warehouseId, variantId, null, 1, 2);
        var secondPage = await service.GetMovementsAsync(warehouseId, variantId, null, 2, 2);

        Assert.True(firstPage.Success, firstPage.Error);
        Assert.True(secondPage.Success, secondPage.Error);
        Assert.Equal(3, firstPage.Data!.TotalCount);
        Assert.Equal(2, firstPage.Data.TotalPages);
        Assert.Equal(new[] { thirdId, secondId }, firstPage.Data.Items.Select(movement => movement.Id));
        Assert.Equal(new decimal?[] { 9m, 7m }, firstPage.Data.Items.Select(movement => movement.RunningBalance));
        Assert.Equal("Reso Cliente", firstPage.Data.Items[0].Reason);
        Assert.Equal("Rettifica Inventario", firstPage.Data.Items[1].Reason);
        var onlyMovementOnSecondPage = Assert.Single(secondPage.Data!.Items);
        Assert.Equal(firstId, onlyMovementOnSecondPage.Id);
        Assert.Equal(10m, onlyMovementOnSecondPage.RunningBalance);
        Assert.Equal("Carico Fornitore", onlyMovementOnSecondPage.Reason);
    }

    [Fact]
    public async Task GetStocksAsync_AggregatesMovementReservationsAndIncomingOrders()
    {
        var database = postgres.CreateDatabase();
        Guid warehouseId;
        Guid secondaryWarehouseId;
        Guid variantId;
        DateTime movementCutoff;

        await using (var seed = database.CreateDbContext())
        {
            var supplier = new Contact("Fornitore inventario test");
            supplier.EnableAsSupplier();
            var customer = new Contact("Cliente inventario test");
            seed.AddRange(supplier, customer);
            await seed.SaveChangesAsync();

            var address = new ContactAddress(
                supplier.Id,
                AddressPurpose.LegalHeadquarters,
                "Via Test 1",
                "20100",
                "Milano");
            seed.Add(address);
            await seed.SaveChangesAsync();

            var code = Guid.NewGuid().ToString("N");
            var warehouse = new Warehouse(
                $"INV-{code}"[..12],
                "Deposito inventario test",
                WarehouseType.Standard,
                address.Id,
                isSellable: true,
                isDefault: true);
            var secondaryWarehouse = new Warehouse(
                $"INV2-{code}"[..12],
                "Deposito secondario inventario test",
                WarehouseType.Standard,
                address.Id,
                isSellable: true,
                isDefault: false);
            var product = new Product(supplier.Id, code, code, "Articolo inventario", 22m, 8m);
            seed.AddRange(warehouse, secondaryWarehouse, product);
            await seed.SaveChangesAsync();

            var variant = new ProductVariant(
                product.Id,
                $"BAR-{code}",
                8m,
                supplier.Id,
                supplierCode: "SKU-STOCK-17");
            seed.Add(variant);
            await seed.SaveChangesAsync();

            var stock = new StockItem(warehouse.Id, variant.Id);
            stock.ApplyMovement(5m);
            stock.SetUnitCost(8.125m);
            var secondaryStock = new StockItem(secondaryWarehouse.Id, variant.Id);
            secondaryStock.ApplyMovement(7m);
            var receipt = new StockMovement(
                variant.Id,
                warehouse.Id,
                StockMovementReason.SupplierReceipt,
                5m);
            seed.AddRange(stock, secondaryStock, receipt);
            await seed.SaveChangesAsync();
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_movements SET unit_cost = {8.125m} WHERE id = {receipt.Id}");
            movementCutoff = receipt.MovementDateUtc.AddSeconds(-1);

            var order = new Order(customer.Id, $"ORD-{code}") { Status = OrderStatus.Confirmed };
            seed.Add(order);
            await seed.SaveChangesAsync();
            seed.Add(new OrderItem(order.Id, variant.Id, 3, 10m, 22m));

            var ddt = new DdtDocument(customer.Id, $"DDT-{code}") { OrderId = order.Id };
            seed.Add(ddt);
            await seed.SaveChangesAsync();
            seed.Add(new DdtItem(ddt.Id, variant.Id, 1, 10m, 22m));

            var purchaseOrder = new SupplierPurchaseOrder
            {
                SupplierId = supplier.Id,
                OrderNumber = $"PO-{code}",
                OrderDate = DateTime.UtcNow,
                Status = SupplierPurchaseOrderStatus.Confirmed
            };
            seed.Add(purchaseOrder);
            await seed.SaveChangesAsync();
            seed.Add(new SupplierPurchaseOrderLine
            {
                SupplierPurchaseOrderId = purchaseOrder.Id,
                ArticleVariantId = variant.Id,
                QuantityOrdered = 10,
                QuantityReceived = 2,
                UnitCost = 8m
            });
            await seed.SaveChangesAsync();

            warehouseId = warehouse.Id;
            secondaryWarehouseId = secondaryWarehouse.Id;
            variantId = variant.Id;
        }

        await using var db = database.CreateDbContext();
        var service = new InventoryStockQueryService(db);

        var filter = new InventoryStockFilterRequest
        {
            WarehouseId = warehouseId,
            Search = "SKU-STOCK",
            Page = 1,
            PageSize = 20
        };
        var current = await service.GetStocksAsync(filter);
        var page = await service.GetStocksPageAsync(filter);
        var summary = await service.GetStocksSummaryAsync(filter);

        Assert.True(current.Success, current.Error);
        Assert.True(page.Success, page.Error);
        Assert.True(summary.Success, summary.Error);
        var row = Assert.Single(current.Data!.Items);
        Assert.Equal(5m, row.QuantityOnHand);
        Assert.Equal(2m, row.QuantityReserved);
        Assert.Equal(8m, row.QuantityIncoming);
        Assert.Equal(11m, row.QuantityAvailable);
        Assert.Equal(8.125m, row.UnitCost);
        Assert.Equal(40.63m, row.TotalValue);
        Assert.Equal(current.Data.Items, page.Data!.Items);
        Assert.Equal(current.Data.Items.Sum(item => item.TotalValue ?? 0m), summary.Data!.Totals.TotalValue);
        Assert.Equal(current.Data.TotalCount, summary.Data!.TotalCount);
        Assert.Equal(current.Data.TotalPages, summary.Data.TotalPages);
        Assert.Equal(current.Data.Totals, summary.Data.Totals);

        var allWarehouses = await service.GetStocksPageAsync(new InventoryStockFilterRequest
        {
            Page = 1,
            PageSize = 20
        });
        Assert.True(allWarehouses.Success, allWarehouses.Error);
        var secondaryWarehouseRow = Assert.Single(
            allWarehouses.Data!.Items,
            item => item.WarehouseId == secondaryWarehouseId);
        Assert.Equal(0m, secondaryWarehouseRow.QuantityReserved);
        Assert.Equal(0m, secondaryWarehouseRow.QuantityIncoming);

        var historical = await service.GetStocksAsync(new InventoryStockFilterRequest
        {
            WarehouseId = warehouseId,
            AtDate = movementCutoff,
            Page = 1,
            PageSize = 20
        });

        Assert.True(historical.Success, historical.Error);
        Assert.Equal(0m, Assert.Single(historical.Data!.Items).QuantityOnHand);

        var legacyAdjustment = new StockMovement(
            variantId,
            warehouseId,
            StockMovementReason.InventoryAdjustment,
            -1m);
        db.StockMovements.Add(legacyAdjustment);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE stock_movements SET quantity_delta = NULL WHERE id = {legacyAdjustment.Id}");

        var ambiguousHistory = await service.GetStocksAsync(new InventoryStockFilterRequest
        {
            WarehouseId = warehouseId,
            AtDate = movementCutoff,
            Page = 1,
            PageSize = 20
        });

        Assert.False(ambiguousHistory.Success);
        Assert.Contains("non conserva la direzione", ambiguousHistory.Error);
    }

    [Fact]
    public async Task GetAmbiguousMovementsAsync_ProposesOnlySingleExactDeltaAndDoesNotModifyHistory()
    {
        var database = postgres.CreateDatabase();
        Guid warehouseId;
        Guid recoverableMovementId;
        Guid proposedMovementId;
        Guid firstUnresolvedMovementId;
        Guid secondUnresolvedMovementId;

        await using (var seed = database.CreateDbContext())
        {
            var supplier = new Contact("Fornitore riconciliazione test");
            supplier.EnableAsSupplier();
            seed.Add(supplier);
            await seed.SaveChangesAsync();

            var address = new ContactAddress(
                supplier.Id,
                AddressPurpose.LegalHeadquarters,
                "Via Test 1",
                "20100",
                "Milano");
            seed.Add(address);
            await seed.SaveChangesAsync();

            var code = Guid.NewGuid().ToString("N");
            var warehouse = new Warehouse(
                $"REC-{code}"[..12],
                "Deposito riconciliazione test",
                WarehouseType.Standard,
                address.Id,
                isSellable: true,
                isDefault: true);
            var product = new Product(supplier.Id, code, code, "Articolo riconciliazione", 22m, 9m);
            seed.AddRange(warehouse, product);
            await seed.SaveChangesAsync();

            var firstVariant = new ProductVariant(product.Id, $"REC-A-{code}", 9m, supplier.Id);
            var secondVariant = new ProductVariant(product.Id, $"REC-B-{code}", 9m, supplier.Id);
            seed.AddRange(firstVariant, secondVariant);
            await seed.SaveChangesAsync();

            var recoverableMovement = new StockMovement(
                firstVariant.Id,
                StockMovementType.PurchaseReceipt,
                5,
                9m);
            var proposedMovement = new StockMovement(
                firstVariant.Id,
                warehouse.Id,
                StockMovementReason.InventoryAdjustment,
                2m);
            var knownMovementForSecondVariant = new StockMovement(
                secondVariant.Id,
                warehouse.Id,
                StockMovementReason.SupplierReceipt,
                4m);
            var firstUnresolvedMovement = new StockMovement(
                secondVariant.Id,
                warehouse.Id,
                StockMovementReason.InventoryAdjustment,
                1m);
            var secondUnresolvedMovement = new StockMovement(
                secondVariant.Id,
                warehouse.Id,
                StockMovementReason.InventoryAdjustment,
                2m);

            var firstStock = new StockItem(warehouse.Id, firstVariant.Id);
            firstStock.ApplyMovement(7m);
            var secondStock = new StockItem(warehouse.Id, secondVariant.Id);
            secondStock.ApplyMovement(10m);
            seed.AddRange(
                recoverableMovement,
                proposedMovement,
                knownMovementForSecondVariant,
                firstUnresolvedMovement,
                secondUnresolvedMovement,
                firstStock,
                secondStock);
            await seed.SaveChangesAsync();

            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_movements SET quantity_delta = NULL, warehouse_id = {warehouse.Id} WHERE id = {recoverableMovement.Id}");

            foreach (var movement in new[]
                     {
                         proposedMovement,
                         firstUnresolvedMovement,
                         secondUnresolvedMovement
                     })
            {
                await seed.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE stock_movements SET quantity_delta = NULL WHERE id = {movement.Id}");
            }

            warehouseId = warehouse.Id;
            recoverableMovementId = recoverableMovement.Id;
            proposedMovementId = proposedMovement.Id;
            firstUnresolvedMovementId = firstUnresolvedMovement.Id;
            secondUnresolvedMovementId = secondUnresolvedMovement.Id;
        }

        await using var db = database.CreateDbContext();
        var service = new InventoryStockQueryService(db);
        var result = await service.GetAmbiguousMovementsAsync(warehouseId);

        Assert.True(result.Success, result.Error);
        Assert.Equal(4, result.Data!.TotalCount);

        var recoverable = Assert.Single(result.Data.Items, item => item.MovementId == recoverableMovementId);
        Assert.False(recoverable.DirectionIsAmbiguous);
        Assert.Equal(5m, recoverable.DirectionKnownFromType);

        var proposed = Assert.Single(result.Data.Items, item => item.MovementId == proposedMovementId);
        Assert.True(proposed.DirectionIsAmbiguous);
        Assert.Equal(5m, proposed.KnownMovementsQuantity);
        Assert.Equal(7m, proposed.StockQuantityOnHand);
        Assert.Equal(2m, proposed.UnexplainedQuantity);
        Assert.Equal(2m, proposed.ProposedQuantityDelta);

        var unresolved = result.Data.Items
            .Where(item => item.MovementId == firstUnresolvedMovementId ||
                           item.MovementId == secondUnresolvedMovementId)
            .ToArray();
        Assert.Equal(2, unresolved.Length);
        Assert.All(unresolved, item => Assert.Null(item.ProposedQuantityDelta));

        var unchangedMovements = await db.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Id == recoverableMovementId ||
                movement.Id == proposedMovementId ||
                movement.Id == firstUnresolvedMovementId ||
                movement.Id == secondUnresolvedMovementId)
            .ToListAsync();
        Assert.All(unchangedMovements, movement => Assert.Null(movement.QuantityDelta));
    }
}
