using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class OrderDraftItemRemovalPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task RemovesDraftItemPersistsDeletionAndRecalculatesTotals()
    {
        var database = postgres.CreateDatabase();
        var calculator = new ItalianAccountingCalculator();
        Guid orderId;
        Guid removedItemId;
        Guid remainingItemId;

        await using (var setup = database.CreateDbContext())
        {
            var contact = new Contact("Cliente");
            var product = new Product(Guid.NewGuid(), "FORN", $"PR-{Guid.NewGuid():N}", "Articolo", 22m, 10m);
            var variant = new ProductVariant(product.Id, $"BC-{Guid.NewGuid():N}", 5m, product.SupplierId)
            {
                Product = product
            };
            setup.AddRange(contact, product, variant);
            await setup.SaveChangesAsync();

            var order = new Order(contact.Id, "ORD-REMOVE") { Contact = contact };
            var removedItem = new OrderItem(order.Id, variant.Id, 2, 10m, 22m);
            var remainingItem = new OrderItem(order.Id, variant.Id, 1, 10m, 22m);
            Assert.True(order.AddItem(removedItem, calculator).Success);
            Assert.True(order.AddItem(remainingItem, calculator).Success);
            setup.Add(order);
            await setup.SaveChangesAsync();

            orderId = order.Id;
            removedItemId = removedItem.Id;
            remainingItemId = remainingItem.Id;
        }

        await using (var update = database.CreateDbContext())
        using (var cache = new MemoryCache(new MemoryCacheOptions()))
        {
            var service = CreateOrderService(update, cache, calculator);
            var result = await service.RemoveDraftItemAsync(orderId, removedItemId);

            Assert.True(result.Success, result.Error);
            Assert.Single(result.Data!.Items);
            Assert.Equal(remainingItemId, result.Data.Items[0].Id);
            Assert.Equal(10.00m, result.Data.TaxableTotal);
            Assert.Equal(2.20m, result.Data.VatTotal);
            Assert.Equal(12.20m, result.Data.GrossTotal);
        }

        await using var verify = database.CreateDbContext();
        Assert.False(await verify.OrderItems.AnyAsync(item => item.Id == removedItemId));
        Assert.True(await verify.OrderItems.AnyAsync(item => item.Id == remainingItemId));
    }

    private static OrderService CreateOrderService(
        MauwDbContext db,
        IMemoryCache cache,
        ItalianAccountingCalculator calculator) =>
        new(
            new UnitOfWork(db, cache),
            new CreateOrderRequestValidator(),
            new CreateOrderItemRequestValidator(),
            calculator,
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            NullLogger<OrderService>.Instance,
            Mock.Of<IDocumentPricingService>());
}
