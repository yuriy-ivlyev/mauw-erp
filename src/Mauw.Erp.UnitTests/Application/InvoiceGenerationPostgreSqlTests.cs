using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class InvoiceGenerationPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task GeneratesContiguousMonthlyInvoiceNumbersUnderConcurrencyAndDoesNotConsumeForInvalidOrder()
    {
        var database = postgres.CreateDatabase();
        var calculator = new ItalianAccountingCalculator();
        Guid invalidOrderId;
        Guid firstOrderId;
        Guid secondOrderId;

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

            var invalid = CreateCompletedOrder(contact, variant, "ORD-INVALID", calculator, invalidFiscalLine: true);
            var first = CreateCompletedOrder(contact, variant, "ORD-FIRST", calculator, shippingCost: 10m);
            var second = CreateCompletedOrder(contact, variant, "ORD-SECOND", calculator);
            setup.AddRange(invalid, first, second);
            MarkCompleted(setup, invalid);
            MarkCompleted(setup, first);
            MarkCompleted(setup, second);
            await setup.SaveChangesAsync();
            invalidOrderId = invalid.Id;
            firstOrderId = first.Id;
            secondOrderId = second.Id;
        }

        var invalidResult = await GenerateAsync(database, calculator, invalidOrderId);
        Assert.False(invalidResult.Success);

        var results = await Task.WhenAll(
            GenerateAsync(database, calculator, firstOrderId),
            GenerateAsync(database, calculator, secondOrderId));
        Assert.All(results, result => Assert.True(result.Success, result.Error));

        var generated = results.Select(result => result.Data!).OrderBy(invoice => invoice.InvoiceNumber).ToArray();
        var period = DateTime.UtcNow.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal($"FAT-{period}-00001", generated[0].InvoiceNumber);
        Assert.Equal($"FAT-{period}-00002", generated[1].InvoiceNumber);
        Assert.Equal(2, generated.Select(invoice => invoice.InvoiceNumber).Distinct().Count());
        Assert.All(generated, invoice => Assert.Equal("Confirmed", invoice.Status));
        Assert.Contains(generated, invoice =>
            invoice.ShippingCost == 10m && invoice.GrossTotal == 24.40m);

        await using var verify = database.CreateDbContext();
        Assert.False(await verify.Invoices.AnyAsync(invoice => invoice.OrderId == invalidOrderId));
    }

    [Fact]
    public async Task SynchronizesCompletedOrderShippingAndHonorsFutureSdiLock()
    {
        var database = postgres.CreateDatabase();
        var calculator = new ItalianAccountingCalculator();
        Guid orderId;

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

            var order = CreateCompletedOrder(contact, variant, "ORD-SYNC", calculator);
            setup.Add(order);
            MarkCompleted(setup, order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        var generated = await GenerateAsync(database, calculator, orderId);
        Assert.True(generated.Success, generated.Error);
        Assert.Equal(12.20m, generated.Data!.GrossTotal);

        await using var update = database.CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var unitOfWork = new UnitOfWork(update, cache);
        var orderToUpdate = await unitOfWork.Orders.GetByIdAsync(orderId);
        Assert.NotNull(orderToUpdate);
        var originalLineId = Assert.Single(orderToUpdate.Items).Id;
        var invoice = await unitOfWork.Invoices.GetByOrderIdAsync(orderId);
        Assert.NotNull(invoice);
        var preparedDocument = calculator.Calculate([new DocumentLineInput(1m, 10m, 22m)]);
        Assert.True(preparedDocument.Success, preparedDocument.Error);
        var pricing = new Mock<IDocumentPricingService>();
        pricing.Setup(service => service.PrepareAsync(
                orderToUpdate.ContactId,
                It.IsAny<IReadOnlyList<DocumentPricingRequest>>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountingResult<PreparedDocumentPricing>.Ok(new(
                [new PreparedPricingLine(
                    preparedDocument.Data!.Lines[0],
                    new PricingSnapshot(null, null, 10m, 10m, false, null, false, null))],
                preparedDocument.Data)));
        var orderService = new OrderService(
            unitOfWork,
            new CreateOrderRequestValidator(),
            new CreateOrderItemRequestValidator(),
            calculator,
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            NullLogger<OrderService>.Instance,
            pricing.Object);

        var updateResult = await orderService.UpdateCompletedInvoiceShippingChargeAsync(orderId,
            new UpdateOrderShippingChargeRequest(10m, 22m, null));
        Assert.True(updateResult.Success, updateResult.Error);
        Assert.Equal(24.40m, updateResult.Data!.GrossTotal);
        Assert.Equal(10m, updateResult.Data.ShippingCost);

        var added = await orderService.AddCompletedBillingItemAsync(orderId,
            new AddCompletedOrderItemRequest(orderToUpdate.Items.First().VariantId, 1, null));
        Assert.True(added.Success, added.Error);
        Assert.Equal(36.60m, added.Data!.GrossTotal);
        Assert.Equal(2, invoice.Items.Count);

        var removed = await orderService.RemoveCompletedBillingItemAsync(orderId, originalLineId);
        Assert.True(removed.Success, removed.Error);
        Assert.Equal(24.40m, removed.Data!.GrossTotal);
        Assert.Single(invoice.Items);
        Assert.True(orderToUpdate.Items.Single(item => item.Id == originalLineId).IsExcludedFromInvoice);

        update.Entry(invoice).Property(value => value.SdiSubmittedAt).CurrentValue = DateTime.UtcNow;
        await update.SaveChangesAsync();
        var blocked = await orderService.UpdateCompletedInvoiceShippingChargeAsync(orderId,
            new UpdateOrderShippingChargeRequest(20m, 22m, null));
        Assert.False(blocked.Success);
        Assert.Equal(24.40m, invoice.GrossTotal);
        Assert.Equal(10m, invoice.ShippingCost);
    }

    private static Order CreateCompletedOrder(
        Contact contact,
        ProductVariant variant,
        string number,
        ItalianAccountingCalculator calculator,
        bool invalidFiscalLine = false,
        decimal shippingCost = 0m)
    {
        var order = new Order(contact.Id, number) { Contact = contact };
        if (invalidFiscalLine)
        {
            order.Items.Add(new OrderItem(order.Id, variant.Id, 1, 10m, 0m));
            order.TaxableTotal = 10m;
            order.GrossTotal = 10m;
            return order;
        }

        Assert.True(order.AddItem(new(order.Id, variant.Id, 1, 10m, 22m), calculator).Success);
        if (shippingCost > 0m)
            Assert.True(order.SetShippingCharge(shippingCost, 22m, null, calculator).Success);
        return order;
    }

    private static void MarkCompleted(MauwDbContext db, Order order) =>
        db.Entry(order).Property(value => value.Status).CurrentValue = OrderStatus.Completed;

    private static async Task<OperationResult<Mauw.Erp.Contracts.Dtos.InvoiceDto>> GenerateAsync(
        PostgreSqlTestDatabase database,
        ItalianAccountingCalculator calculator,
        Guid orderId)
    {
        await using var db = database.CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new InvoiceService(
            new UnitOfWork(db, cache),
            calculator,
            Mock.Of<Mauw.Erp.Application.Services.ICompanySettingsService>(),
            Mock.Of<Mauw.Erp.Contracts.Interfaces.IInvoicePdfGenerator>());
        return await service.GenerateFromOrderAsync(orderId);
    }
}
