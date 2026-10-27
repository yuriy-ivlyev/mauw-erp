using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class OrderNumberPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task OrderCreationContinuesExistingMonthlyPrefixesAndFormats()
    {
        var database = postgres.CreateDatabase();
        var contact = new Contact("Cliente numerazione test");
        var prefix = $"ORD-{DateTime.UtcNow:MMyyyy}-";
        var quotePrefix = $"PRV-{DateTime.UtcNow:MMyyyy}-";
        await using (var seed = database.CreateDbContext())
        {
            seed.Add(contact);
            await seed.SaveChangesAsync();
            seed.AddRange(
                new Order(contact.Id, $"{prefix}001") { OrderType = OrderType.CassaSession },
                new Order(contact.Id, $"{prefix}002"),
                new Order(contact.Id, $"{quotePrefix}001") { OrderType = OrderType.Preventivo });
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);
        var standard = await service.CreateOrderAsync(new() { ContactId = contact.Id });
        var quote = await service.CreateOrderAsync(new()
        {
            ContactId = contact.Id,
            OrderType = nameof(OrderType.Preventivo)
        });

        Assert.True(standard.Success, standard.Error);
        Assert.Equal($"{prefix}003", standard.Data!.OrderNumber);
        Assert.True(quote.Success, quote.Error);
        Assert.Equal($"{quotePrefix}002", quote.Data!.OrderNumber);
    }

    [Fact]
    public async Task ConcurrentStandardAndCassaOrderNumberAssignmentsAreUniqueAndSequential()
    {
        var database = postgres.CreateDatabase();
        var contact = new Contact("Cliente concorrenza numerazione");
        await using (var seed = database.CreateDbContext())
        {
            seed.Add(contact);
            await seed.SaveChangesAsync();
        }

        var prefix = $"ORD-{DateTime.UtcNow:MMyyyy}-";
        const int requestCount = 12;
        var assignments = await Task.WhenAll(Enumerable.Range(0, requestCount).Select(async index =>
        {
            await using var db = database.CreateDbContext();
            var order = new Order(contact.Id, prefix)
            {
                OrderType = index % 2 == 0 ? OrderType.Standard : OrderType.CassaSession
            };
            await new OrderRepository(db).AddWithNextNumberAsync(order, prefix);
            return order.OrderNumber;
        }));

        Assert.Equal(requestCount, assignments.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, requestCount).Select(sequence => $"{prefix}{sequence:D3}").Order(),
            assignments.Order());

        await using var verify = database.CreateDbContext();
        Assert.Equal(requestCount, await verify.Orders.CountAsync(order => order.OrderNumber.StartsWith(prefix)));
        Assert.All(assignments, number => Assert.Matches(@"^ORD-\d{6}-\d{3}$", number));
    }

    private static OrderService CreateOrderService(MauwDbContext db)
    {
        var calculator = new ItalianAccountingCalculator();
        return new OrderService(
            new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
            new CreateOrderRequestValidator(),
            new CreateOrderItemRequestValidator(),
            calculator,
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            NullLogger<OrderService>.Instance,
            Mock.Of<IDocumentPricingService>());
    }
}
