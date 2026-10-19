using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class PriceListServiceTests
{
    [Fact]
    public async Task CrudPreservesRuleIdsAndDeletionSetsCustomerReferenceNull()
    {
        using var fixture = new Fixture();
        var created = await fixture.Service.SaveAsync(null, new()
        {
            Name = "Ingrosso", Rules = [new() { CalculationType = PriceCalculationType.FixedPrice, Value = 10.12345678m }]
        }, default);
        Assert.True(created.Success, created.Error);
        var id = created.Data!.Id;
        var ruleId = Assert.Single(created.Data.Rules).Id;
        fixture.Customer.CustomerProfile!.PriceListId = id;
        await fixture.Db.SaveChangesAsync();
        var updated = await fixture.Service.SaveAsync(id, new()
        {
            Name = "Ingrosso 2",
            Rules = [created.Data.Rules[0] with { Value = 11m }]
        }, default);
        Assert.True(updated.Success, updated.Error);
        Assert.Equal(ruleId, Assert.Single(updated.Data!.Rules).Id);
        Assert.Single(await fixture.Db.PriceListRules.ToListAsync());
        var appended = await fixture.Service.SaveAsync(id, new()
        {
            Name = "Ingrosso 3",
            Rules = [updated.Data.Rules[0], new() { Value = 15m, MinimumQuantity = 10m, Priority = 20 }]
        }, default);
        Assert.True(appended.Success, appended.Error);
        Assert.Equal(2, await fixture.Db.PriceListRules.CountAsync());
        Assert.True((await fixture.Service.SetActivationAsync(id, false, default)).Success);
        Assert.False((await fixture.Service.GetAsync(id, default))!.IsActive);
        Assert.True((await fixture.Service.DeleteAsync(id, default)).Success);
        Assert.Null(fixture.Customer.CustomerProfile.PriceListId);
        Assert.Empty(await fixture.Db.PriceListRules.ToListAsync());
        Assert.NotNull(await fixture.Db.Contacts.FindAsync(fixture.Customer.Id));
    }

    [Fact]
    public async Task MissingTargetsAndInvalidUpdateDoNotChangeTrackedList()
    {
        using var fixture = new Fixture();
        var original = await fixture.Service.SaveAsync(null, new() { Name = "Original" }, default);
        var failed = await fixture.Service.SaveAsync(original.Data!.Id, new()
        {
            Name = "Changed", Rules = [new() { Value = 1m, VariantId = Guid.NewGuid() }]
        }, default);
        Assert.False(failed.Success);
        Assert.Equal("Original", (await fixture.Service.GetAsync(original.Data.Id, default))!.Name);
    }

    [Fact]
    public async Task BatchLoadsOneRuleTreeAndOneCatalogForOneThousandLines()
    {
        var calculator = new ItalianAccountingCalculator();
        var repository = new Mock<IPriceListRepository>();
        var list = PriceList.Create("Test", "EUR", true,
            [new(null, PriceCalculationType.PercentageDiscount, 10m, MinimumQuantity: 0.25m)]).Data!;
        var variant = new ProductVariant(Guid.NewGuid(), "SKU", 10m)
        {
            SellingPrice = 20m,
            Product = new(Guid.NewGuid(), "S", "P", "Product", 100m, 22m, 10m)
        };
        using var db = CreateDb();
        db.Add(variant);
        repository.Setup(repo => repo.GetAsync(list.Id, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        repository.Setup(repo => repo.GetVariantsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { variant });
        var service = new PriceListService(repository.Object, new(calculator), new());
        var result = await service.ResolveAsync(new()
        {
            PriceListId = list.Id,
            Lines = Enumerable.Range(0, 1000).Select(_ => new ResolvePriceLineRequest(variant.Id, 0.5m)).ToList()
        }, default);
        Assert.True(result.Success, result.Error);
        Assert.Equal(1000, result.Data!.Lines.Count);
        Assert.All(result.Data.Lines, line => Assert.Equal(18m, line.AgreedUnitNetPrice));
        repository.Verify(repo => repo.GetAsync(list.Id, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.GetVariantsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CustomerDefaultPriceAndVatGroupsArePersistedWithoutDoubleDiscount()
    {
        using var fixture = new Fixture();
        await fixture.AssignListAsync(PriceCalculationType.PercentageDiscount, 10m);
        var prepared = await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [new(fixture.Variant.Id, 5m, null, null, null, null, null)], DateTimeOffset.UtcNow);
        Assert.True(prepared.Success, prepared.Error);
        var line = Assert.Single(prepared.Data!.Lines);
        Assert.Equal(10m, line.FiscalLine.UnitNetPrice);
        Assert.Equal(10m, line.FiscalLine.DiscountPercent);
        Assert.Equal(9m, line.Snapshot.AgreedUnitNetPrice);
        Assert.Equal(45m, line.FiscalLine.LineNetTotal);
        Assert.Equal(54.90m, prepared.Data.FiscalDocument.TotalGross);
        Assert.NotNull(line.Snapshot.PriceListId);
        Assert.False(line.Snapshot.IsManualOverride);
    }

    [Fact]
    public async Task SnapshotsSurviveRuleChangesDeletionAndReload()
    {
        using var fixture = new Fixture();
        var listId = await fixture.AssignListAsync(PriceCalculationType.FixedPrice, 0.03m);
        var pricing = await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [new(fixture.Variant.Id, 1m, null, null, null, null, null),
             new(fixture.Variant.Id, 1m, null, null, null, null, null)], DateTimeOffset.UtcNow);
        Assert.True(pricing.Success, pricing.Error);
        var invoice = new Invoice(fixture.Customer.Id, "F-1");
        foreach (var prepared in pricing.Data!.Lines)
            Assert.True(invoice.AddItem(new(invoice.Id, fixture.Variant.Id, 1, prepared.FiscalLine.UnitNetPrice,
                prepared.FiscalLine.VatRate, discountPercent: prepared.FiscalLine.DiscountPercent)
                { PricingSnapshot = prepared.Snapshot }, fixture.Calculator).Success);
        fixture.Db.Add(invoice);
        await fixture.Db.SaveChangesAsync();
        var id = invoice.Id;
        Assert.Equal(0.01m, invoice.VatTotal);
        await fixture.Service.SaveAsync(listId, new() { Name = "Changed" }, default);
        await fixture.Service.DeleteAsync(listId, default);
        fixture.Db.ChangeTracker.Clear();
        var loaded = await fixture.Db.Invoices.Include(item => item.Items).SingleAsync(item => item.Id == id);
        Assert.Equal(0.07m, loaded.GrossTotal);
        Assert.All(loaded.Items, line =>
        {
            Assert.Equal(0.03m, line.UnitNetPrice);
            Assert.Equal(listId, line.PricingSnapshot!.PriceListId);
            Assert.Equal(0.03m, line.PricingSnapshot.AgreedUnitNetPrice);
            Assert.Null(line.LineVatTotal);
        });
    }

    [Fact]
    public async Task ManualOverrideRequiresReasonAndPreservesOriginalQuotation()
    {
        using var fixture = new Fixture();
        await fixture.AssignListAsync(PriceCalculationType.FixedPrice, 12m);
        DocumentPricingRequest request = new(fixture.Variant.Id, 1m, 8m, null, 22m, null, 0m);
        Assert.False((await fixture.Pricing.PrepareAsync(fixture.Customer.Id, [request], DateTimeOffset.UtcNow)).Success);
        var result = await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [request with { OverrideReason = "Prezzo concordato" }], DateTimeOffset.UtcNow);
        Assert.True(result.Success, result.Error);
        var line = Assert.Single(result.Data!.Lines);
        Assert.True(line.Snapshot.IsManualOverride);
        Assert.Equal(12m, line.Snapshot.ListUnitPrice);
        Assert.Equal(8m, line.Snapshot.AgreedUnitNetPrice);
    }

    [Fact]
    public async Task ReturnCopiesInvoicePriceWithoutRepricingEvenWithInactiveList()
    {
        using var fixture = new Fixture();
        var id = await fixture.AssignListAsync(PriceCalculationType.FixedPrice, 100m);
        var invoice = new Invoice(fixture.Customer.Id, "ORIGIN");
        var source = new InvoiceItem(invoice.Id, fixture.Variant.Id, 5, 18.83606557m, 22m);
        Assert.True(invoice.AddItem(source, fixture.Calculator).Success);
        fixture.Db.Add(invoice);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.SetActivationAsync(id, false, default);
        var result = await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [new(fixture.Variant.Id, -5m, null, null, null, null, null,
                IsCustomerReturn: true, SourceLineId: source.Id)], DateTimeOffset.UtcNow);
        Assert.True(result.Success, result.Error);
        var line = Assert.Single(result.Data!.Lines);
        Assert.Equal(source.UnitNetPrice, line.FiscalLine.UnitNetPrice);
        Assert.Equal(-94.18m, line.FiscalLine.LineNetTotal);
        Assert.Equal(-114.90m, result.Data.FiscalDocument.TotalGross);
        Assert.True(line.Snapshot.IsCustomerReturn);
        Assert.Equal(source.Id, line.Snapshot.SourceLineId);
        Assert.Null(line.Snapshot.AgreedUnitNetPrice);
        var denied = await fixture.Pricing.PrepareAsync(Guid.NewGuid(),
            [new(fixture.Variant.Id, -5m, null, null, null, null, null,
                IsCustomerReturn: true, SourceLineId: source.Id)], DateTimeOffset.UtcNow);
        Assert.False(denied.Success);
    }

    [Fact]
    public async Task ReturnWithoutInvoiceIsRejectedEvenWithForcedPriceAndReason()
    {
        using var fixture = new Fixture();
        DocumentPricingRequest request = new(fixture.Variant.Id, -1m, null, null, 0m, null, 0m, IsCustomerReturn: true);
        Assert.False((await fixture.Pricing.PrepareAsync(fixture.Customer.Id, [request], DateTimeOffset.UtcNow)).Success);
        request = request with { UnitNetPrice = 10m, OverrideReason = "Reso senza riferimento" };
        Assert.False((await fixture.Pricing.PrepareAsync(fixture.Customer.Id, [request], DateTimeOffset.UtcNow)).Success);
        request = request with { NatureCode = "N3.2" };
        var result = await fixture.Pricing.PrepareAsync(fixture.Customer.Id, [request], DateTimeOffset.UtcNow);
        Assert.False(result.Success);
        Assert.Contains("fattura di origine", result.Error);
    }

    [Fact]
    public async Task ReturnCopiesExistingPriceSnapshotAndRejectsManualPrice()
    {
        using var fixture = new Fixture();
        var invoice = new Invoice(fixture.Customer.Id, "ORIGIN");
        var snapshot = new PricingSnapshot(Guid.NewGuid(), Guid.NewGuid(), 25m, 18.83606557m,
            true, "Accordo originale", false, null);
        var source = new InvoiceItem(invoice.Id, fixture.Variant.Id, 5, 20.92896174m, 22m, discountPercent: 10m)
            { PricingSnapshot = snapshot };
        Assert.True(invoice.AddItem(source, fixture.Calculator).Success);
        fixture.Db.Add(invoice);
        await fixture.Db.SaveChangesAsync();
        DocumentPricingRequest request = new(fixture.Variant.Id, -2m, null, null, null, null, null,
            IsCustomerReturn: true, SourceLineId: source.Id);
        var result = await fixture.Pricing.PrepareAsync(fixture.Customer.Id, [request], DateTimeOffset.UtcNow);
        Assert.True(result.Success, result.Error);
        var line = Assert.Single(result.Data!.Lines);
        Assert.Equal(source.UnitNetPrice, line.FiscalLine.UnitNetPrice);
        Assert.Equal(source.DiscountPercent, line.FiscalLine.DiscountPercent);
        Assert.Equal(snapshot.AgreedUnitNetPrice, line.Snapshot.AgreedUnitNetPrice);
        Assert.Equal(source.Id, line.Snapshot.SourceLineId);
        Assert.False((await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [request with { UnitNetPrice = 1m, OverrideReason = "Forzato" }], DateTimeOffset.UtcNow)).Success);
    }

    [Fact]
    public async Task EmissionFromSavedOrderCopiesSnapshotEvenAfterListDeactivation()
    {
        using var fixture = new Fixture();
        var listId = await fixture.AssignListAsync(PriceCalculationType.PercentageDiscount, 10m);
        var pricing = await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [new(fixture.Variant.Id, 5m, null, null, null, null, null)], DateTimeOffset.UtcNow);
        Assert.True(pricing.Success, pricing.Error);
        var source = Assert.Single(pricing.Data!.Lines);
        var order = new Order(fixture.Customer.Id, "ORD-1");
        var item = new OrderItem(order.Id, fixture.Variant.Id, 5, source.FiscalLine.UnitNetPrice,
            source.FiscalLine.VatRate, discountPercent: source.FiscalLine.DiscountPercent)
            { PricingSnapshot = source.Snapshot };
        Assert.True(order.AddItem(item, fixture.Calculator).Success);
        fixture.Db.Add(order);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.SetActivationAsync(listId, false, default);
        DocumentPricingRequest request = new(fixture.Variant.Id, 5m, null, null, 22m, null, null,
            SourceOrderItemId: item.Id);
        var emitted = await fixture.Pricing.PrepareAsync(fixture.Customer.Id, [request], DateTimeOffset.UtcNow);
        Assert.True(emitted.Success, emitted.Error);
        var copied = Assert.Single(emitted.Data!.Lines);
        Assert.Equal(source.Snapshot, copied.Snapshot);
        Assert.NotSame(source.Snapshot, copied.Snapshot);
        Assert.Equal(order.GrossTotal, emitted.Data.FiscalDocument.TotalGross);
        Assert.False((await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [request with { VatRate = 10m }], DateTimeOffset.UtcNow)).Success);
        Assert.False((await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [request with { Quantity = 6m }], DateTimeOffset.UtcNow)).Success);
        Assert.False((await fixture.Pricing.PrepareAsync(Guid.NewGuid(), [request], DateTimeOffset.UtcNow)).Success);
    }

    [Fact]
    public async Task CartAutoPricingIgnoresClientPriceButManualPriceIsExplicitAndTracked()
    {
        using var fixture = new Fixture();
        await fixture.AssignListAsync(PriceCalculationType.FixedPrice, 12m);
        DocumentPricingRequest request = new(fixture.Variant.Id, 1m, 999m, null, 22m, null, 99m,
            PreserveLegacyPrice: true);
        var automatic = await fixture.Pricing.PrepareAsync(fixture.Customer.Id, [request], DateTimeOffset.UtcNow);
        Assert.True(automatic.Success, automatic.Error);
        Assert.Equal(12m, automatic.Data!.FiscalDocument.TotalTaxable);
        Assert.False(Assert.Single(automatic.Data.Lines).Snapshot.IsManualOverride);
        var manual = await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [request with { PreserveLegacyPrice = false, UnitNetPrice = 8m, DiscountPercent = 0m,
                OverrideReason = "Trattativa" }], DateTimeOffset.UtcNow);
        Assert.True(manual.Success, manual.Error);
        Assert.Equal(8m, manual.Data!.FiscalDocument.TotalTaxable);
        Assert.True(Assert.Single(manual.Data.Lines).Snapshot.IsManualOverride);
    }

    [Fact]
    public async Task FallbackAndUnknownVariantsAreHandledExplicitly()
    {
        using var fixture = new Fixture();
        var result = await fixture.Service.ResolveAsync(new()
        {
            ContactId = fixture.Customer.Id, Lines = [new(fixture.Variant.Id, 1m)]
        }, default);
        Assert.True(result.Success, result.Error);
        Assert.Null(result.Data!.PriceListId);
        Assert.Equal(10m, Assert.Single(result.Data.Lines).AgreedUnitNetPrice);
        Assert.False((await fixture.Pricing.PrepareAsync(fixture.Customer.Id,
            [new(Guid.NewGuid(), 1m, 1m, null, 22m, null, null)], DateTimeOffset.UtcNow)).Success);
    }

    [Fact]
    public void PostgreSqlModelHasSeparateCompositeIndexesAndInformationalSnapshotsWithoutForeignKeys()
    {
        using var db = new MauwDbContext(new DbContextOptionsBuilder<MauwDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        var rules = db.Model.FindEntityType(typeof(PriceListRule))!;
        foreach (var target in new[] { "VariantId", "ProductId", "CategoryId" })
            Assert.Contains(rules.GetIndexes(), index =>
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { "PriceListId", target }));
        var customer = db.Model.FindEntityType(typeof(CustomerProfile))!;
        Assert.Equal(DeleteBehavior.SetNull, customer.GetForeignKeys()
            .Single(key => key.Properties.Any(p => p.Name == "PriceListId")).DeleteBehavior);
        var snapshot = db.Model.GetEntityTypes().First(type => type.ClrType == typeof(PricingSnapshot));
        Assert.Single(snapshot.GetForeignKeys());
        Assert.All(snapshot.GetForeignKeys(), key => Assert.True(key.IsOwnership));
    }

    private static MauwDbContext CreateDb() => new(new DbContextOptionsBuilder<MauwDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class Fixture : IDisposable
    {
        public MauwDbContext Db { get; } = CreateDb();
        public ItalianAccountingCalculator Calculator { get; } = new();
        public PriceListService Service { get; }
        public DocumentPricingService Pricing { get; }
        public Contact Customer { get; } = new("Cliente");
        public ProductVariant Variant { get; }

        public Fixture()
        {
            Customer.EnableAsCustomer();
            Db.Add(Customer);
            var product = new Product(Guid.NewGuid(), "S", "P", "Articolo", 0m, 22m, 10m);
            Db.Add(product);
            Variant = new(product.Id, "SKU", 10m) { Product = product, SellingPrice = 10m };
            Db.Add(Variant);
            Db.SaveChanges();
            var repository = new PriceListRepository(Db);
            Service = new(repository, new(Calculator), new());
            Pricing = new(repository, Service, Calculator);
        }

        public async Task<Guid> AssignListAsync(PriceCalculationType type, decimal value)
        {
            var result = await Service.SaveAsync(null, new()
            {
                Name = "Listino", Rules = [new() { CalculationType = type, Value = value }]
            }, default);
            Assert.True(result.Success, result.Error);
            Customer.CustomerProfile!.PriceListId = result.Data!.Id;
            await Db.SaveChangesAsync();
            return result.Data.Id;
        }

        public void Dispose() => Db.Dispose();
    }
}
