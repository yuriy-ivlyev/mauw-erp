using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
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
