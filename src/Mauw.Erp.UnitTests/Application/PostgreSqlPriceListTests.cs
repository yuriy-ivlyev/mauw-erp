using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class PostgreSqlPriceListTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task VariantSupplierCodeCanRepeatAcrossSuppliersButNotWithinOneSupplier()
    {
        await using var db = postgres.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var supplierA = Guid.NewGuid();
        var supplierB = Guid.NewGuid();
        var productA = new Product(supplierA, "PRODUCT", $"A-{Guid.NewGuid():N}", "Articolo A", 0m, 22m, 10m);
        var productB = new Product(supplierB, "PRODUCT", $"B-{Guid.NewGuid():N}", "Articolo B", 0m, 22m, 10m);
        var secondProductA = new Product(supplierA, "PRODUCT-2", $"A2-{Guid.NewGuid():N}", "Articolo A2", 0m, 22m, 10m);
        db.AddRange(productA, productB, secondProductA);
        await db.SaveChangesAsync();

        db.AddRange(
            new ProductVariant(productA.Id, $"A-{Guid.NewGuid():N}", 10m, supplierA, supplierCode: "VARIANT"),
            new ProductVariant(productB.Id, $"B-{Guid.NewGuid():N}", 10m, supplierB, supplierCode: "VARIANT"));
        await db.SaveChangesAsync();

        db.Add(new ProductVariant(secondProductA.Id, $"A2-{Guid.NewGuid():N}", 10m, supplierA, supplierCode: "VARIANT"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task PrecisionSnapshotReloadAndDatabaseSetNullUseRealPostgreSql()
    {
        await using var db = postgres.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Countries.FindAsync("IT") is null)
            db.Countries.Add(new() { IsoCode = "IT", Name = "Italia" });
        var customer = new Contact("Pricing integration test");
        customer.EnableAsCustomer();
        db.Add(customer);
        var code = $"TEST-{Guid.NewGuid():N}";
        var product = new Product(Guid.NewGuid(), code, code, "Articolo test", 0m, 22m, 10m)
            { SellingPrice = 23.12345678m };
        db.Add(product);
        var variant = new ProductVariant(product.Id, code, 10m, product.SupplierId)
            { Product = product, SellingPrice = 17.12345678m };
        db.Add(variant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var persistedVariant = await db.ProductVariants.Include(item => item.Product).SingleAsync(item => item.Id == variant.Id);
        Assert.Equal(23.12345678m, persistedVariant.Product.SellingPrice);
        Assert.Equal(17.12345678m, persistedVariant.SellingPrice);
        var catalogPrice = new CatalogPriceCalculator().GetStoredPrice(persistedVariant);
        Assert.True(catalogPrice.Success, catalogPrice.Error);
        Assert.Equal(17.12345678m, catalogPrice.Data!.Value);
        persistedVariant.PurchaseCost = 999m;
        persistedVariant.Product.DefaultMarkupRate = 200.25m;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        persistedVariant = await db.ProductVariants.Include(item => item.Product).SingleAsync(item => item.Id == variant.Id);
        Assert.Equal(17.12345678m, persistedVariant.SellingPrice);
        Assert.Null(persistedVariant.MarkupRate);
        Assert.Equal(17.12345678m, new CatalogPriceCalculator().GetStoredPrice(persistedVariant).Data!.Value);
        var calculator = new ItalianAccountingCalculator();
        var repository = new PriceListRepository(db);
        var service = new PriceListService(repository, new(calculator), new());
        var pricing = new DocumentPricingService(repository, service, calculator);
        var created = await service.SaveAsync(null, new()
        {
            Name = code,
            Rules = [new()
            {
                CalculationType = PriceCalculationType.FixedPrice, Value = 18.83606557m,
                VariantId = variant.Id, MinimumQuantity = 0.25m,
                ValidFrom = DateTimeOffset.UtcNow.AddDays(-1)
            }]
        }, default);
        Assert.True(created.Success, created.Error);
        var listId = created.Data!.Id;
        (await db.CustomerProfiles.SingleAsync(profile => profile.ContactId == customer.Id)).PriceListId = listId;
        await db.SaveChangesAsync();
        var prepared = await pricing.PrepareAsync(customer.Id,
            [new(variant.Id, 5m, null, null, null, null, null)], DateTimeOffset.UtcNow);
        Assert.True(prepared.Success, prepared.Error);
        var line = Assert.Single(prepared.Data!.Lines);
        var invoice = new Invoice(customer.Id, code);
        Assert.True(invoice.AddItem(new(invoice.Id, variant.Id, 5, line.FiscalLine.UnitNetPrice, 22m)
            { PricingSnapshot = line.Snapshot }, calculator).Success);
        db.Add(invoice);
        await db.SaveChangesAsync();
        var invoiceId = invoice.Id;
        var customerId = customer.Id;
        db.ChangeTracker.Clear();

        var updated = await service.SaveAsync(listId, new()
        {
            Name = code, Rules = [created.Data.Rules[0] with { Value = 25.12345678m }]
        }, default);
        Assert.True(updated.Success, updated.Error);
        Assert.True((await service.DeleteAsync(listId, default)).Success);
        db.ChangeTracker.Clear();

        var persisted = await db.Invoices.Include(item => item.Items).SingleAsync(item => item.Id == invoiceId);
        var persistedLine = Assert.Single(persisted.Items);
        Assert.Equal(18.83606557m, persistedLine.UnitNetPrice);
        Assert.Equal(18.83606557m, persistedLine.PricingSnapshot!.AgreedUnitNetPrice);
        Assert.Equal(listId, persistedLine.PricingSnapshot.PriceListId);
        Assert.Equal(94.18m, persisted.TaxableTotal);
        Assert.Equal(20.72m, persisted.VatTotal);
        Assert.Equal(114.90m, persisted.GrossTotal);
        Assert.Null((await db.CustomerProfiles.SingleAsync(profile => profile.ContactId == customerId)).PriceListId);
        Assert.Empty(await db.PriceListRules.Where(rule => rule.PriceListId == listId).ToListAsync());
    }

}
