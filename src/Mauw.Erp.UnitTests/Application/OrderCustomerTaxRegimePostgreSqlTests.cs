using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
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
public sealed class OrderCustomerTaxRegimePostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    private static readonly string[] Countries = ["IT", "DE", "FR", "GR", "CH", "US", "GB"];

    [Theory]
    [InlineData("DE", "N3.2")]
    [InlineData("FR", "N3.2")]
    [InlineData("GR", "N3.2")]
    [InlineData("CH", "N3.1")]
    [InlineData("US", "N3.1")]
    [InlineData("GB", "N3.1")]
    public async Task CreateOrderRejectsATaxableShippingRateForAForeignCustomer(string country, string nature)
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);

        var wrongRate = await service.CreateOrderAsync(new()
        {
            ContactId = seed.Customers[country], ShippingCost = 10.00m, ShippingVatRate = 22m
        });
        var wrongNature = await service.CreateOrderAsync(new()
        {
            ContactId = seed.Customers[country], ShippingCost = 10.00m, ShippingVatRate = 0m,
            ShippingNatureCode = nature == "N3.2" ? "N3.1" : "N3.2"
        });

        Assert.False(wrongRate.Success);
        Assert.Contains("aliquota IVA ammessa è 0%", wrongRate.Error);
        Assert.Contains(nature, wrongRate.Error);
        Assert.False(wrongNature.Success);
        Assert.Contains($"Natura IVA ammessa è {nature}", wrongNature.Error);
        await using var verify = database.CreateDbContext();
        Assert.Empty(await verify.Orders.ToListAsync());
    }

    [Theory]
    [InlineData("DE", "N3.2")]
    [InlineData("FR", "N3.2")]
    [InlineData("GR", "N3.2")]
    [InlineData("CH", "N3.1")]
    [InlineData("US", "N3.1")]
    [InlineData("GB", "N3.1")]
    public async Task CreateOrderAcceptsTheZeroRateShippingWithTheRegimeNature(string country, string nature)
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);

        var explicitNature = await service.CreateOrderAsync(new()
        {
            ContactId = seed.Customers[country], ShippingCost = 10.00m, ShippingVatRate = 0m, ShippingNatureCode = nature
        });
        var defaulted = await service.CreateOrderAsync(new()
        {
            ContactId = seed.Customers[country], ShippingCost = 12.50m
        });

        Assert.True(explicitNature.Success, explicitNature.Error);
        Assert.True(defaulted.Success, defaulted.Error);
        await using var verify = database.CreateDbContext();
        var orders = await verify.Orders.OrderBy(order => order.ShippingCost).ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.All(orders, order =>
        {
            Assert.Equal(0m, order.ShippingVatRate);
            Assert.Equal(nature, order.ShippingNatureCode);
            Assert.Equal(0.00m, order.VatTotal);
            Assert.Equal(order.ShippingCost, order.GrossTotal);
        });
    }

    [Fact]
    public async Task CreateOrderKeepsTheRequestedShippingRateForADomesticCustomer()
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();

        var result = await CreateOrderService(db).CreateOrderAsync(new()
        {
            ContactId = seed.Customers["IT"], ShippingCost = 10.00m, ShippingVatRate = 22m
        });

        Assert.True(result.Success, result.Error);
        Assert.Equal(22m, result.Data!.ShippingVatRate);
        Assert.Equal(2.20m, result.Data.VatTotal);
        Assert.Equal(12.20m, result.Data.GrossTotal);
    }

    [Fact]
    public async Task CreateOrderWithoutShippingChargeDoesNotRequireAClassification()
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();

        var result = await CreateOrderService(db).CreateOrderAsync(new() { ContactId = seed.Customers["DE"] });

        Assert.True(result.Success, result.Error);
        Assert.Null(result.Data!.ShippingVatRate);
    }

    [Fact]
    public async Task UpdateShippingChargeAppliesTheSameRuleToTheShippingOfAnExistingDraft()
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);
        var us = (await service.CreateOrderAsync(new() { ContactId = seed.Customers["US"] })).Data!;

        var rejected = await service.UpdateShippingChargeAsync(us.Id, new(10.00m, 22m, null));
        var wrongNature = await service.UpdateShippingChargeAsync(us.Id, new(10.00m, 0m, "N3.2"));
        var accepted = await service.UpdateShippingChargeAsync(us.Id, new(10.00m, 0m, "N3.1"));
        var defaulted = await service.UpdateShippingChargeAsync(us.Id, new(15.00m, null, null));

        Assert.False(rejected.Success);
        Assert.False(wrongNature.Success);
        Assert.True(accepted.Success, accepted.Error);
        Assert.True(defaulted.Success, defaulted.Error);
        Assert.Equal(0m, defaulted.Data!.ShippingVatRate);
        Assert.Equal("N3.1", defaulted.Data.ShippingNatureCode);
        Assert.Equal(15.00m, defaulted.Data.GrossTotal);
    }

    [Theory]
    [InlineData("DE", "N3.2")]
    [InlineData("GR", "N3.2")]
    [InlineData("CH", "N3.1")]
    [InlineData("US", "N3.1")]
    public async Task AddItemRejectsTaxableRatesAndMismatchingNaturesForAForeignCustomer(string country, string nature)
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);
        var order = (await service.CreateOrderAsync(new() { ContactId = seed.Customers[country] })).Data!;

        var taxable = await service.AddItemAsync(order.Id, Line(seed, vatRate: 22m));
        var wrongNature = await service.AddItemAsync(order.Id, Line(
            seed, vatRate: 0m, nature: nature == "N3.2" ? "N3.1" : "N3.2"));

        Assert.False(taxable.Success);
        Assert.Contains("aliquota IVA ammessa è 0%", taxable.Error);
        Assert.False(wrongNature.Success);
        Assert.Contains($"Natura IVA ammessa è {nature}", wrongNature.Error);
        await using var verify = database.CreateDbContext();
        Assert.Empty(await verify.OrderItems.ToListAsync());
    }

    [Theory]
    [InlineData("DE", "N3.2")]
    [InlineData("FR", "N3.2")]
    [InlineData("GR", "N3.2")]
    [InlineData("CH", "N3.1")]
    [InlineData("US", "N3.1")]
    [InlineData("GB", "N3.1")]
    public async Task AddItemAcceptsOrDefaultsTheZeroRateClassificationForAForeignCustomer(string country, string nature)
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);
        var order = (await service.CreateOrderAsync(new() { ContactId = seed.Customers[country] })).Data!;

        var explicitLine = await service.AddItemAsync(order.Id, Line(seed, vatRate: 0m, nature: nature));
        // The article is taxed at 22%: with nothing requested the server forces the regime classification.
        var defaulted = await service.AddItemAsync(order.Id, Line(seed));

        Assert.True(explicitLine.Success, explicitLine.Error);
        Assert.True(defaulted.Success, defaulted.Error);
        var items = defaulted.Data!.Items;
        Assert.Equal(2, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal(0m, item.VatRate);
            Assert.Equal(nature, item.NatureCode);
        });
        Assert.Equal(40.00m, defaulted.Data.TaxableTotal);
        Assert.Equal(0.00m, defaulted.Data.VatTotal);
        Assert.Equal(40.00m, defaulted.Data.GrossTotal);
    }

    [Fact]
    public async Task AddItemUsesTheArticleRateForADomesticCustomer()
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);
        var order = (await service.CreateOrderAsync(new() { ContactId = seed.Customers["IT"] })).Data!;

        var result = await service.AddItemAsync(order.Id, Line(seed));

        Assert.True(result.Success, result.Error);
        Assert.Equal(22m, Assert.Single(result.Data!.Items).VatRate);
        Assert.Equal(20.00m, result.Data.TaxableTotal);
        Assert.Equal(4.40m, result.Data.VatTotal);
        Assert.Equal(24.40m, result.Data.GrossTotal);
    }

    [Fact]
    public async Task UpdateItemAppliesTheSameRuleToTheEditedDraftLine()
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);
        var order = (await service.CreateOrderAsync(new() { ContactId = seed.Customers["FR"] })).Data!;
        var added = await service.AddItemAsync(order.Id, Line(seed, vatRate: 0m, nature: "N3.2"));
        Assert.True(added.Success, added.Error);
        var itemId = added.Data!.Items.Single().Id;

        var rejected = await service.UpdateItemAsync(order.Id, itemId, Line(seed, vatRate: 22m));
        var updated = await service.UpdateItemAsync(order.Id, itemId, Line(seed, vatRate: 0m, nature: "N3.2"));

        Assert.False(rejected.Success);
        Assert.True(updated.Success, updated.Error);
        Assert.Equal(0.00m, updated.Data!.VatTotal);
    }

    [Theory]
    [InlineData("DE", "N3.2")]
    [InlineData("GR", "N3.2")]
    [InlineData("CH", "N3.1")]
    [InlineData("US", "N3.1")]
    public async Task PreviewForcesTheForeignClassificationBeforeComputingTheVatBreakdown(string country, string nature)
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();

        var result = await CreateOrderService(db).PreviewOrderAsync(new(
            seed.Customers[country],
            [new CassaCartItemRequest(seed.VariantId, 2, 100m, 22m, 2)],
            ShippingCost: 10.00m, ShippingVatRate: 22m));

        Assert.True(result.Success, result.Error);
        var preview = result.Data!;
        Assert.Equal(0.00m, preview.TotalVat);
        Assert.Equal(210.00m, preview.TotalTaxable);
        Assert.Equal(210.00m, preview.TotalGross);
        var group = Assert.Single(preview.VatSummary);
        Assert.Equal(0m, group.VatRate);
        Assert.Equal(nature, group.NatureCode);
        Assert.Equal(0.00m, group.VatAmount);
        Assert.All(preview.Lines, line =>
        {
            Assert.Equal(0m, line.VatRate);
            Assert.Equal(nature, line.NatureCode);
        });
    }

    [Fact]
    public async Task PreviewKeepsTheRequestedRatesForADomesticCustomerAndRejectsUnknownCustomers()
    {
        var (database, seed) = await SeedAsync();
        await using var db = database.CreateDbContext();
        var service = CreateOrderService(db);

        var domestic = await service.PreviewOrderAsync(new(
            seed.Customers["IT"],
            [new CassaCartItemRequest(seed.VariantId, 2, 100m, 22m, 2)],
            ShippingCost: 10.00m, ShippingVatRate: 22m));
        var missing = await service.PreviewOrderAsync(new(
            Guid.NewGuid(), [new CassaCartItemRequest(seed.VariantId, 1, 100m, 22m, 1)]));

        Assert.True(domestic.Success, domestic.Error);
        Assert.Equal(46.20m, domestic.Data!.TotalVat);
        Assert.Equal(256.20m, domestic.Data.TotalGross);
        Assert.Equal(22m, Assert.Single(domestic.Data.VatSummary).VatRate);
        Assert.False(missing.Success);
        Assert.Contains("not found", missing.Error);
    }

    private async Task<(PostgreSqlTestDatabase Database, Seed Seed)> SeedAsync()
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        var customers = Countries.ToDictionary(
            country => country,
            country => new Contact($"Cliente {country}") { CountryCode = country });
        foreach (var customer in customers.Values)
        {
            customer.EnableAsCustomer();
            db.Add(customer);
        }

        var code = $"TAX-{Guid.NewGuid():N}";
        var product = new Product(Guid.NewGuid(), code, code, "Articolo regime fiscale", 22m, 10m);
        var variant = new ProductVariant(product.Id, code, 10m, product.SupplierId) { Product = product };
        db.AddRange(product, variant);
        await db.SaveChangesAsync();
        return (database, new Seed(customers.ToDictionary(pair => pair.Key, pair => pair.Value.Id), variant.Id));
    }

    private static CreateOrderItemRequest Line(Seed seed, decimal? vatRate = null, string? nature = null) => new()
    {
        VariantId = seed.VariantId, Quantity = 2, UnitNetPrice = 10m, VatRate = vatRate, NatureCode = nature
    };

    private static OrderService CreateOrderService(MauwDbContext db)
    {
        var calculator = new ItalianAccountingCalculator();
        var repository = new PriceListRepository(db);
        var pricing = new DocumentPricingService(
            repository, new PriceListService(repository, new(calculator), new()), calculator);
        return new OrderService(
            new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
            new CreateOrderRequestValidator(),
            new CreateOrderItemRequestValidator(),
            calculator,
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            NullLogger<OrderService>.Instance,
            pricing);
    }

    private sealed record Seed(IReadOnlyDictionary<string, Guid> Customers, Guid VariantId);
}
