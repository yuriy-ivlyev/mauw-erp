using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Commands.Orders;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Enums;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class DdtAndCassaStockMovementPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task AddingDdtItemDecrementsStockAndCreatesShipmentMovement()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using var db = database.CreateDbContext();

        var result = await DdtService(db).AddItemAsync(seed.DdtId, DdtItemRequest(seed.VariantId, 2));

        Assert.True(result.Success, result.Error);
        Assert.Equal(seed.WarehouseId, result.Data!.WarehouseId);
        Assert.Equal(3m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal(StockMovementType.SalesDelivery, movement.Type);
        Assert.Equal(StockMovementReason.OrderShipment, movement.Reason);
        Assert.Equal(-2m, movement.QuantityDelta);
        Assert.Equal(seed.DdtId, movement.ReferenceId);
        Assert.Equal(seed.WarehouseId, movement.WarehouseId);
    }

    [Fact]
    public async Task InsufficientStockDoesNotMutateDdtOrInventory()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using var db = database.CreateDbContext();

        var result = await DdtService(db).AddItemAsync(seed.DdtId, DdtItemRequest(seed.VariantId, 6));

        Assert.False(result.Success);
        Assert.Contains("Giacenza insufficiente", result.Error);
        Assert.Empty(await db.DdtItems.ToListAsync());
        Assert.Empty(await db.StockMovements.ToListAsync());
        Assert.Equal(5m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        var ddt = await db.DdtDocuments.SingleAsync();
        Assert.Equal(0m, ddt.GrossTotal);
    }

    [Fact]
    public async Task CustomerReturnDdtAddsStockAndCreatesPositiveReturnMovement()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid sourceLineId;
        await using (var setup = database.CreateDbContext())
        {
            var invoice = new Invoice(seed.ContactId, "FATT-ORIGINE");
            var sourceLine = new InvoiceItem(invoice.Id, seed.VariantId, 2, 10m, 22m)
            {
                LineNetTotal = 20m,
                PricingSnapshot = new PricingSnapshot(null, null, 10m, null, false, null, false, null)
            };
            invoice.Items.Add(sourceLine);
            setup.Add(invoice);
            await setup.SaveChangesAsync();
            sourceLineId = sourceLine.Id;
        }

        await using var db = database.CreateDbContext();
        var result = await DdtService(db).AddItemAsync(seed.DdtId, new CreateOrderItemRequest
        {
            VariantId = seed.VariantId,
            Quantity = 1,
            IsCustomerReturn = true,
            SourceLineId = sourceLineId
        });

        Assert.True(result.Success, result.Error);
        Assert.Equal(6m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal(StockMovementType.CustomerReturn, movement.Type);
        Assert.Equal(StockMovementReason.CustomerReturn, movement.Reason);
        Assert.Equal(1m, movement.QuantityDelta);
    }

    [Fact]
    public async Task FailedSaveRollsBackDdtItemsMovementsAndStockTogether()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using (var db = database.CreateDbContext(new ThrowAfterSave()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DdtService(db).AddItemAsync(seed.DdtId, DdtItemRequest(seed.VariantId, 2)));
        }

        await using var verify = database.CreateDbContext();
        Assert.Empty(await verify.DdtItems.ToListAsync());
        Assert.Empty(await verify.StockMovements.ToListAsync());
        Assert.Equal(5m, (await verify.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(0m, (await verify.DdtDocuments.SingleAsync()).GrossTotal);
    }

    [Fact]
    public async Task DeletingDdtReversesStockMovementAndRestoresAvailability()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using var db = database.CreateDbContext();
        var service = DdtService(db);

        Assert.True((await service.AddItemAsync(seed.DdtId, DdtItemRequest(seed.VariantId, 2))).Success);
        Assert.True((await service.DeleteDdtAsync(seed.DdtId)).Success);

        Assert.Equal(5m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        var movements = await db.StockMovements.OrderBy(x => x.QuantityDelta).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, movement => movement.QuantityDelta == -2m);
        Assert.Contains(movements, movement => movement.QuantityDelta == 2m);
        Assert.Empty(await db.DdtDocuments.ToListAsync());
    }

    [Fact]
    public async Task ConfirmingCassaSaleAndReturnRecordsCorrectStockDirections()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid saleOrderId;
        Guid returnOrderId;
        await using (var setup = database.CreateDbContext())
        {
            saleOrderId = await AddCassaOrderAsync(setup, seed, 2, isCustomerReturn: false);
            returnOrderId = await AddCassaOrderAsync(setup, seed, 1, isCustomerReturn: true);
        }

        await using var db = database.CreateDbContext();
        var service = OrderService(db);
        Assert.True((await service.ConfirmOrderAsync(saleOrderId)).Success);
        Assert.True((await service.ConfirmOrderAsync(returnOrderId)).Success);
        Assert.False((await service.ConfirmOrderAsync(saleOrderId)).Success);

        Assert.Equal(4m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        var movements = await db.StockMovements.OrderBy(x => x.Reason).ToListAsync();
        Assert.Collection(movements,
            sale =>
            {
                Assert.Equal(StockMovementType.SalesDelivery, sale.Type);
                Assert.Equal(StockMovementReason.ShowroomSale, sale.Reason);
                Assert.Equal(-2m, sale.QuantityDelta);
            },
            customerReturn =>
            {
                Assert.Equal(StockMovementType.CustomerReturn, customerReturn.Type);
                Assert.Equal(StockMovementReason.CustomerReturn, customerReturn.Reason);
                Assert.Equal(1m, customerReturn.QuantityDelta);
            });
    }

    [Fact]
    public async Task FinalizingCashSaleReturnsReceiptAndCommitsOrderAndStockTogether()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid orderId;
        await using (var setup = database.CreateDbContext())
        {
            var order = new Order(
                seed.ContactId,
                $"CASH-{Guid.NewGuid():N}",
                fulfillmentType: OrderFulfillmentType.CashDesk);
            order.Items.Add(new OrderItem(order.Id, seed.VariantId, 2, 10m, 22m)
            {
                LineNetTotal = 20m
            });
            order.GrossTotal = 24.4m;
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var db = database.CreateDbContext();
        var orderPdf = new Mock<IOrderService>();
        orderPdf.Setup(service => service.GetOrderPdfAsync(orderId))
            .ReturnsAsync(OperationResult<byte[]>.Ok([1, 2, 3]));
        var handler = new FinalizeCashSaleCommandHandler(
            new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
            orderPdf.Object);
        var result = await handler.Handle(
            new FinalizeCashSaleCommand(orderId, "Carta", "transaction-123"),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(OrderStatusEnum.Completed, result.Data!.Order.Status);
        Assert.Equal("CashDesk", result.Data.Order.FulfillmentType);
        Assert.StartsWith("ricevuta-CASH-", result.Data.DocumentFileName);
        Assert.Equal("application/pdf", result.Data.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Data.DocumentBytes);
        Assert.Equal(3m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal(StockMovementReason.ShowroomSale, movement.Reason);
        Assert.Equal(-2m, movement.QuantityDelta);
        var persistedOrder = await db.Orders.SingleAsync();
        Assert.Equal("Carta", persistedOrder.PaymentMethod);
        Assert.Equal("transaction-123", persistedOrder.PaymentTransactionDetails);
        orderPdf.Verify(service => service.GetOrderPdfAsync(orderId), Times.Once);
    }

    [Fact]
    public async Task StandardShippingReturnsDdtPdfAndCommitsDdtAndStockTogether()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid orderId;
        await using (var setup = database.CreateDbContext())
        {
            var order = new Order(seed.ContactId, $"SHIP-{Guid.NewGuid():N}");
            order.Items.Add(new OrderItem(order.Id, seed.VariantId, 2, 10m, 22m)
            {
                LineNetTotal = 20m
            });
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var db = database.CreateDbContext())
        {
            var service = OrderService(db);
            var confirmed = await service.ConfirmOrderAsync(orderId);
            Assert.True(confirmed.Success, confirmed.Error);
            var stock = await db.StockItems.SingleAsync();
            Assert.Equal(5m, stock.QuantityOnHand);
            Assert.Equal(2m, stock.QuantityReserved);

            var order = await db.Orders.Include(item => item.Items).SingleAsync(item => item.Id == orderId);
            order.StartPicking(Guid.NewGuid());
            order.CompletePacking(1, 2.5m);
            await db.SaveChangesAsync();

            var ddtPdf = new Mock<IDdtService>();
            ddtPdf.Setup(service => service.GetDdtPdfAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.Ok([4, 5, 6]));
            var handler = new CreateShipmentCommandHandler(
                new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
                new ItalianAccountingCalculator(),
                ddtPdf.Object);
            var shipped = await handler.Handle(
                new CreateShipmentCommand(orderId, "Corriere", "TRACK-456", BoxCount: 1),
                CancellationToken.None);

            Assert.True(shipped.Success, shipped.Error);
            Assert.Equal(OrderStatusEnum.Shipped, shipped.Data!.Order.Status);
            Assert.StartsWith($"DDT-{DateTime.UtcNow:yyyy}-", shipped.Data.Shipment.DdtNumber);
            Assert.StartsWith($"SPED-{DateTime.UtcNow:yyyy}-", shipped.Data.Shipment.ShipmentNumber);
            Assert.Equal(2, shipped.Data.Shipment.Lines.Single().QuantityShipped);
            Assert.Equal(2, shipped.Data.Order.Items.Single().ShippedQuantity);
            Assert.Equal(0, shipped.Data.Order.Items.Single().RemainingQuantity);
            Assert.StartsWith("ddt-", shipped.Data.DocumentFileName);
            Assert.Equal("application/pdf", shipped.Data.ContentType);
            Assert.Equal(new byte[] { 4, 5, 6 }, shipped.Data.DocumentBytes);
            stock = await db.StockItems.SingleAsync();
            Assert.Equal(3m, stock.QuantityOnHand);
            Assert.Equal(0m, stock.QuantityReserved);
            var ddt = await db.DdtDocuments.Include(item => item.Items)
                .SingleAsync(item => item.OrderId == orderId);
            Assert.Equal(orderId, ddt.OrderId);
            Assert.Equal(shipped.Data.Shipment.Id, ddt.ShipmentId);
            Assert.Single(ddt.Items);
            var shipment = await db.Shipments.Include(item => item.Lines)
                .SingleAsync(item => item.OrderId == orderId);
            Assert.Equal(ddt.DdtNumber, shipment.DdtNumber);
            Assert.Equal(1, shipment.BoxCount);
            Assert.Equal(2.5m, shipment.TotalWeightKg);
            Assert.Equal(2, shipment.Lines.Single().QuantityShipped);
            Assert.Equal(2, (await db.OrderItems.SingleAsync()).QuantityShipped);
            ddtPdf.Verify(service => service.GetDdtPdfAsync(
                ddt.Id, It.IsAny<CancellationToken>()), Times.Once);

            var emittedDdt = DdtService(db);
            Assert.False((await emittedDdt.AddItemAsync(
                ddt.Id, DdtItemRequest(seed.VariantId, 1))).Success);
            Assert.False((await emittedDdt.DeleteDdtAsync(ddt.Id)).Success);
            Assert.Equal(3m, (await db.StockItems.SingleAsync()).QuantityOnHand);
            Assert.Equal(1, await db.Shipments.CountAsync());
        }
    }

    [Fact]
    public async Task PartialShipmentValidatesQuantitiesAndEmitsSequentialDdts()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid orderId;
        Guid orderItemId;
        await using (var setup = database.CreateDbContext())
        {
            var newOrder = new Order(seed.ContactId, $"SHIP-PARTIAL-{Guid.NewGuid():N}");
            var orderItem = new OrderItem(newOrder.Id, seed.VariantId, 3, 10m, 22m)
            {
                LineNetTotal = 30m
            };
            newOrder.Items.Add(orderItem);
            setup.Add(newOrder);
            await setup.SaveChangesAsync();
            orderId = newOrder.Id;
            orderItemId = orderItem.Id;
        }

        await using var db = database.CreateDbContext();
        var orderService = OrderService(db);
        Assert.True((await orderService.ConfirmOrderAsync(orderId)).Success);
        var order = await db.Orders.Include(item => item.Items).SingleAsync(item => item.Id == orderId);
        order.StartPicking(Guid.NewGuid());
        order.CompletePacking(1, 1.5m);
        await db.SaveChangesAsync();

        var queue = await orderService.GetFulfillmentQueueAsync(1, 20);
        Assert.True(queue.Success, queue.Error);
        var queueOrder = Assert.Single(queue.Data!.Items);
        Assert.Equal(3, queueOrder.Items.Single().RemainingQuantity);
        Assert.Equal("Maglia test", queueOrder.Items.Single().ProductName);

        var ddtPdf = new Mock<IDdtService>();
        ddtPdf.Setup(service => service.GetDdtPdfAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<byte[]>.Ok([1]));
        var handler = new CreateShipmentCommandHandler(
            new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
            new ItalianAccountingCalculator(),
            ddtPdf.Object);

        var invalid = await handler.Handle(
            new CreateShipmentCommand(
                orderId, "GLS", null, [new ShipOrderItemRequest(orderItemId, -1)], BoxCount: 1),
            CancellationToken.None);
        Assert.False(invalid.Success);
        Assert.Equal(0, (await db.OrderItems.SingleAsync()).QuantityShipped);
        Assert.Empty(await db.Shipments.ToListAsync());
        Assert.Equal(5m, (await db.StockItems.SingleAsync()).QuantityOnHand);

        var partial = await handler.Handle(
            new CreateShipmentCommand(
                orderId, "GLS", null, [new ShipOrderItemRequest(orderItemId, 2)], BoxCount: 1),
            CancellationToken.None);
        Assert.True(partial.Success, partial.Error);
        Assert.Equal(OrderStatusEnum.PartiallyShipped, partial.Data!.Order.Status);
        Assert.Equal(1, partial.Data.Order.Items.Single().RemainingQuantity);
        Assert.Null(partial.Data.Shipment.TrackingCode);
        Assert.Equal(3m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(1m, (await db.StockItems.SingleAsync()).QuantityReserved);

        var final = await handler.Handle(
            new CreateShipmentCommand(
                orderId, "GLS", null, [new ShipOrderItemRequest(orderItemId, 1)], BoxCount: 1),
            CancellationToken.None);
        Assert.True(final.Success, final.Error);
        Assert.Equal(OrderStatusEnum.Shipped, final.Data!.Order.Status);
        Assert.Equal(0, final.Data.Order.Items.Single().RemainingQuantity);
        var firstNumber = int.Parse(partial.Data.Shipment.DdtNumber.Split('-')[2]);
        var nextNumber = int.Parse(final.Data.Shipment.DdtNumber.Split('-')[2]);
        Assert.Equal(firstNumber + 1, nextNumber);
        Assert.Equal(2, await db.Shipments.CountAsync());
        Assert.Equal(2, await db.DdtDocuments.CountAsync(item => item.OrderId == orderId));
        Assert.Equal(3, (await db.OrderItems.SingleAsync()).QuantityShipped);
        Assert.Equal(2m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(0m, (await db.StockItems.SingleAsync()).QuantityReserved);
    }

    [Fact]
    public async Task CreditLimitRoutesOrderToApprovalBeforeStockIsReserved()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid orderId;
        await using (var setup = database.CreateDbContext())
        {
            var profile = await setup.CustomerProfiles.SingleAsync(item => item.ContactId == seed.ContactId);
            profile.CreditLimit = 20m;

            var order = new Order(seed.ContactId, $"CREDIT-{Guid.NewGuid():N}")
            {
                GrossTotal = 24.4m
            };
            order.Items.Add(new OrderItem(order.Id, seed.VariantId, 2, 10m, 22m)
            {
                LineNetTotal = 20m
            });
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var db = database.CreateDbContext();
        var unitOfWork = new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions()));
        var pending = await OrderService(db).ConfirmOrderAsync(orderId);

        Assert.True(pending.Success, pending.Error);
        Assert.Equal(OrderStatusEnum.PendingApproval, pending.Data!.Status);
        Assert.Equal(0m, (await db.StockItems.SingleAsync()).QuantityReserved);

        var approved = await new ApproveOrderCommandHandler(unitOfWork).Handle(
            new ApproveOrderCommand(orderId), CancellationToken.None);

        Assert.True(approved.Success, approved.Error);
        Assert.Equal(OrderStatusEnum.Confirmed, approved.Data!.Status);
        Assert.Equal(2m, (await db.StockItems.SingleAsync()).QuantityReserved);
    }

    [Fact]
    public async Task CashSaleSaveFailureRollsBackOrderAndStockTogether()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid orderId;
        await using (var setup = database.CreateDbContext())
        {
            var order = new Order(
                seed.ContactId,
                $"CASH-ROLLBACK-{Guid.NewGuid():N}",
                fulfillmentType: OrderFulfillmentType.CashDesk);
            order.Items.Add(new OrderItem(order.Id, seed.VariantId, 2, 10m, 22m));
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var db = database.CreateDbContext(new ThrowAfterSave()))
        {
            var orderPdf = new Mock<IOrderService>();
            orderPdf.Setup(service => service.GetOrderPdfAsync(orderId))
                .ReturnsAsync(OperationResult<byte[]>.Ok([1, 2, 3]));
            var handler = new FinalizeCashSaleCommandHandler(
                new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
                orderPdf.Object);
            var result = await handler.Handle(
                new FinalizeCashSaleCommand(orderId, "Contanti", null),
                CancellationToken.None);
            Assert.False(result.Success);
        }

        await using var verify = database.CreateDbContext();
        Assert.Equal(5m, (await verify.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Empty(await verify.StockMovements.ToListAsync());
        Assert.Equal(OrderStatus.Draft, (await verify.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task CashReceiptGenerationFailureRollsBackOrderAndStockTogether()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid orderId;
        await using (var setup = database.CreateDbContext())
        {
            var order = new Order(
                seed.ContactId,
                $"CASH-PDF-FAIL-{Guid.NewGuid():N}",
                fulfillmentType: OrderFulfillmentType.CashDesk);
            order.Items.Add(new OrderItem(order.Id, seed.VariantId, 2, 10m, 22m));
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var db = database.CreateDbContext())
        {
            var orderPdf = new Mock<IOrderService>();
            orderPdf.Setup(service => service.GetOrderPdfAsync(orderId))
                .ReturnsAsync(OperationResult<byte[]>.Fail("PDF unavailable"));
            var handler = new FinalizeCashSaleCommandHandler(
                new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
                orderPdf.Object);
            var result = await handler.Handle(
                new FinalizeCashSaleCommand(orderId, "Contanti", null),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("PDF unavailable", result.Error);
        }

        await using var verify = database.CreateDbContext();
        Assert.Equal(5m, (await verify.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Empty(await verify.StockMovements.ToListAsync());
        Assert.Equal(OrderStatus.Draft, (await verify.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task DdtPdfGenerationFailureRollsBackShipmentStockAndOrder()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid orderId;
        await using (var setup = database.CreateDbContext())
        {
            var order = new Order(seed.ContactId, $"SHIP-PDF-FAIL-{Guid.NewGuid():N}");
            order.Items.Add(new OrderItem(order.Id, seed.VariantId, 2, 10m, 22m)
            {
                LineNetTotal = 20m
            });
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var db = database.CreateDbContext())
        {
            var service = OrderService(db);
            Assert.True((await service.ConfirmOrderAsync(orderId)).Success);
            var order = await db.Orders.Include(item => item.Items).SingleAsync(item => item.Id == orderId);
            order.StartPicking(Guid.NewGuid());
            order.CompletePacking(1, 2.5m);
            await db.SaveChangesAsync();

            var ddtPdf = new Mock<IDdtService>();
            ddtPdf.Setup(item => item.GetDdtPdfAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.Fail("DDT PDF unavailable"));
            var handler = new CreateShipmentCommandHandler(
                new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
                new ItalianAccountingCalculator(),
                ddtPdf.Object);
            var result = await handler.Handle(
                new CreateShipmentCommand(orderId, "Corriere", "TRACK-FAIL", BoxCount: 1),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("DDT PDF unavailable", result.Error);
        }

        await using var verify = database.CreateDbContext();
        var persistedOrder = await verify.Orders.SingleAsync(item => item.Id == orderId);
        Assert.Equal(OrderStatus.Packing, persistedOrder.Status);
        Assert.Equal(5m, (await verify.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(2m, (await verify.StockItems.SingleAsync()).QuantityReserved);
        Assert.DoesNotContain(
            await verify.DdtDocuments.ToListAsync(),
            ddt => ddt.OrderId == orderId);
        Assert.DoesNotContain(
        await verify.Shipments.ToListAsync(),
        shipment => shipment.OrderId == orderId);
        Assert.Empty(await verify.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task CreatingDdtRequiresAndPersistsWarehouse()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using var db = database.CreateDbContext();
        var service = DdtService(db);

        var invalid = await service.CreateDdtAsync(new()
        {
            ContactId = seed.ContactId,
            DdtNumber = "DDT-NO-WAREHOUSE"
        });
        Assert.False(invalid.Success);

        var created = await service.CreateDdtAsync(new()
        {
            ContactId = seed.ContactId,
            DdtNumber = "DDT-WITH-WAREHOUSE",
            WarehouseId = seed.WarehouseId
        });

        Assert.True(created.Success, created.Error);
        Assert.Equal(seed.WarehouseId, created.Data!.WarehouseId);
    }

    private static DdtService DdtService(MauwDbContext db)
    {
        var calculator = new ItalianAccountingCalculator();
        var pricing = new DocumentPricingService(
            new PriceListRepository(db), Mock.Of<IPriceListService>(), calculator);
        return new DdtService(
            new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
            new CreateOrderItemRequestValidator(),
            calculator,
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            pricing);
    }

    private static OrderService OrderService(MauwDbContext db)
    {
        var calculator = new ItalianAccountingCalculator();
        return new OrderService(
            new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
            Mock.Of<IValidator<CreateOrderRequest>>(),
            new CreateOrderItemRequestValidator(),
            calculator,
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            NullLogger<OrderService>.Instance,
            Mock.Of<IDocumentPricingService>());
    }

    private static CreateOrderItemRequest DdtItemRequest(Guid variantId, int quantity) => new()
    {
        VariantId = variantId,
        Quantity = quantity,
        UnitNetPrice = 10m,
        VatRate = 22m
    };

    private static async Task<Guid> AddCassaOrderAsync(
        MauwDbContext db, Seed seed, int quantity, bool isCustomerReturn)
    {
        var order = new Order(seed.ContactId, $"CASSA-{Guid.NewGuid():N}")
        {
            OrderType = OrderType.CassaSession
        };
        order.Suspend("test");
        var item = new OrderItem(order.Id, seed.VariantId, quantity, 10m, 22m, quantity)
        {
            LineNetTotal = quantity * 10m,
            PricingSnapshot = new PricingSnapshot(
                null, null, 10m, null, false, null, isCustomerReturn,
                isCustomerReturn ? Guid.NewGuid() : null)
        };
        order.Items.Add(item);
        order.TaxableTotal = quantity * 10m;
        order.VatTotal = quantity * 2.2m;
        order.GrossTotal = quantity * 12.2m;
        db.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private static async Task<Seed> SeedAsync(PostgreSqlTestDatabase database)
    {
        await using var db = database.CreateDbContext();
        if (!await db.Countries.AnyAsync(country => country.IsoCode == "IT"))
            db.Countries.Add(new() { IsoCode = "IT", Name = "Italia" });

        var contact = new Contact("Cliente DDT test") { Status = ContactStatus.Active };
        contact.EnableAsCustomer();
        contact.EnableAsSupplier();
        db.Add(contact);
        var address = new ContactAddress(
            contact.Id, AddressPurpose.LegalHeadquarters, "Via Test 1", "20100", "Milano");
        db.Add(address);
        var code = Guid.NewGuid().ToString("N");
        var warehouse = new Warehouse(
            code[..16], "Magazzino DDT test", WarehouseType.Standard, address.Id,
            isSellable: true, isDefault: true);
        db.Add(warehouse);
        var product = new Product(contact.Id, code, code, "Maglia test", 22m, 12m);
        db.Add(product);
        var variant = new ProductVariant(product.Id, code, 12m, contact.Id) { Product = product };
        db.Add(variant);
        var ddt = new DdtDocument(contact.Id, $"DDT-{code}", warehouse.Id);
        db.Add(ddt);
        await db.SaveChangesAsync();
        db.StockItems.Add(new(warehouse.Id, variant.Id, 5m, 12m));
        await db.SaveChangesAsync();
        return new(ddt.Id, contact.Id, warehouse.Id, variant.Id);
    }

    private sealed record Seed(Guid DdtId, Guid ContactId, Guid WarehouseId, Guid VariantId);

    private sealed class ThrowAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected failure after SaveChanges, before commit.");
    }
}
