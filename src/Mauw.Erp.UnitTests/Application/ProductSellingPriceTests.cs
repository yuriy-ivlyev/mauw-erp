using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class ProductSellingPriceTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task OnlyDefinitivePriceIsSavedAndCostChangesNeverRepriceVariants()
    {
        using var fixture = new Fixture(postgres);
        var entered = new PriceFormulaCalculator().Calculate(10m,
            new PriceFormulaDefinition([new("Ricarico", 30m)], 0.01m)).Data!.FinalPrice;
        var created = await fixture.Service.CreateProductAsync(Request(entered));
        Assert.True(created.Success, created.Error);
        var productId = created.Data!.Id;
        var variant = await fixture.Service.CreateProductVariantAsync(productId,
            new(productId, null, null, "SKU", 10m, SellingPrice: entered));
        Assert.True(variant.Success, variant.Error);
        var variantId = variant.Data!.Id;
        var updated = await fixture.Service.UpdateProductVariantAsync(productId, variantId,
            new(null, null, "SKU", 90m, SellingPrice: entered));
        Assert.True(updated.Success, updated.Error);
        Assert.Equal(13m, updated.Data!.SellingPrice);
        await fixture.Service.UpdateProductAsync(productId, Request(500m) with { BasePurchaseCost = 99m });
        fixture.Db.ChangeTracker.Clear();
        var reloaded = await fixture.Service.GetProductAsync(productId);
        Assert.Null(reloaded.Data!.SellingPrice);
        Assert.Equal(13m, Assert.Single(reloaded.Data.Variants).SellingPrice);
        Assert.Equal(13m, (await fixture.Service.GetVariantByBarcodeAsync("SKU")).Data!.UnitNetPrice);
    }

    [Fact]
    public async Task BarcodeMatrixPriceListAndDocumentsUseStoredPriceWithoutRoundingOrMarkup()
    {
        using var fixture = new Fixture(postgres);
        var product = (await fixture.Service.CreateProductAsync(Request(94.18m))).Data!;
        var variant = (await fixture.Service.CreateProductVariantAsync(product.Id,
            new(product.Id, null, null, "BARCODE", 90m, SellingPrice: 18.83606557m))).Data!;
        fixture.Db.ChangeTracker.Clear();
        var barcode = await fixture.Service.GetVariantByBarcodeAsync("BARCODE");
        Assert.Equal(18.83606557m, barcode.Data!.UnitNetPrice);
        var matrix = await fixture.Service.GetProductMatrixAsync(product.Id, Guid.NewGuid());
        Assert.Equal(18.83606557m, Assert.Single(matrix.Data!.Cells).UnitNetPrice);
        var repository = new PriceListRepository(fixture.Db);
        var lists = new PriceListService(repository, new(new()), new());
        var quote = await lists.ResolveAsync(new() { Lines = [new(variant.Id, 5m)] }, default);
        Assert.True(quote.Success, quote.Error);
        Assert.Equal(barcode.Data.UnitNetPrice, Assert.Single(quote.Data!.Lines).ListUnitPrice);
        var prepared = await new DocumentPricingService(repository, lists, new()).PrepareAsync(Guid.NewGuid(),
            [new(variant.Id, 5m, null, null, null, null, null)], DateTimeOffset.UtcNow);
        Assert.True(prepared.Success, prepared.Error);
        Assert.Equal(94.18m, prepared.Data!.FiscalDocument.TotalTaxable);
    }

    [Fact]
    public async Task InvalidChangesDoNotMutateAndMissingPricesCannotBeSaved()
    {
        using var fixture = new Fixture(postgres);
        var product = (await fixture.Service.CreateProductAsync(Request(19m))).Data!;
        var variant = (await fixture.Service.CreateProductVariantAsync(product.Id,
            new(product.Id, null, null, "SKU", 10m, SellingPrice: 15m))).Data!;
        Assert.False((await fixture.Service.UpdateProductAsync(product.Id, Request(-1m) with { HasVariants = false })).Success);
        Assert.False((await fixture.Service.UpdateProductVariantAsync(product.Id, variant.Id,
            new(null, null, "CHANGED", 12m, SellingPrice: null))).Success);
        var existing = await fixture.Db.ProductVariants.FindAsync(variant.Id);
        Assert.Equal("SKU", existing!.Barcode);
        Assert.Equal(10m, existing.PurchaseCost);
        Assert.Equal(15m, existing.SellingPrice);
        Assert.Null((await fixture.Service.GetProductAsync(product.Id)).Data!.SellingPrice);
        Assert.False((await fixture.Service.CreateProductAsync(Request(1.123456789m) with { HasVariants = false })).Success);
        Assert.False((await fixture.Service.CreateProductAsync(Request(null) with { HasVariants = false })).Success);
        Assert.Single(await fixture.Db.Products.ToListAsync());
    }

    [Fact]
    public async Task ProductWithoutVariantsSavesTheExplicitPriceAndDoesNotRecalculateOnCostChange()
    {
        using var fixture = new Fixture(postgres);
        var created = await fixture.Service.CreateProductAsync(Request(18.12345678m) with { HasVariants = false });
        Assert.True(created.Success, created.Error);
        var updated = await fixture.Service.UpdateProductAsync(created.Data!.Id,
            Request(18.12345678m) with { HasVariants = false, BasePurchaseCost = 20m });
        Assert.True(updated.Success, updated.Error);
        Assert.Equal(18.12345678m, Assert.Single(updated.Data!.Variants).SellingPrice);
    }

    [Fact]
    public async Task VariantParentNeedsNoPriceAndVariantPricesRemainIndependent()
    {
        using var fixture = new Fixture(postgres);
        var created = await fixture.Service.CreateProductAsync(Request(null));
        Assert.True(created.Success, created.Error);
        Assert.True(created.Data!.HasVariants);
        Assert.Null(created.Data.SellingPrice);
        Assert.Empty(created.Data.Variants);
        var first = await fixture.Service.CreateProductVariantAsync(created.Data.Id,
            new(created.Data.Id, null, null, "ONE-SIZE", 10m, SellingPrice: 20m));
        Assert.True(first.Success, first.Error);
        var updated = await fixture.Service.UpdateProductAsync(created.Data.Id, Request(999m));
        Assert.True(updated.Success, updated.Error);
        Assert.Null(updated.Data!.SellingPrice);
        Assert.Equal(20m, Assert.Single(updated.Data.Variants).SellingPrice);
        fixture.Db.ChangeTracker.Clear();
        Assert.Null((await fixture.Db.Products.FindAsync(created.Data.Id))!.SellingPrice);
        Assert.False((await fixture.Service.UpdateProductAsync(created.Data.Id,
            Request(30m) with { HasVariants = false })).Success);
    }

    [Fact]
    public async Task VariantCreationRejectsInvalidPriceBeforePersisting()
    {
        using var fixture = new Fixture(postgres);
        var product = await fixture.Service.CreateProductAsync(Request(null));
        Assert.True(product.Success, product.Error);

        var result = await fixture.Service.CreateProductVariantAsync(product.Data!.Id,
            new(product.Data.Id, null, null, "INVALID-PRICE", 10m, SellingPrice: 1.123456789m));

        Assert.False(result.Success);
        Assert.Contains("massimo 8 decimali", result.Error);
        Assert.Empty(await fixture.Db.ProductVariants.ToListAsync());
    }

    [Fact]
    public async Task SimpleProductKeepsOneSkuAndSynchronizesExplicitPriceEdits()
    {
        using var fixture = new Fixture(postgres);
        var created = await fixture.Service.CreateProductAsync(Request(20m) with { HasVariants = false });
        var sku = Assert.Single(created.Data!.Variants);
        Assert.False((await fixture.Service.DeleteProductVariantAsync(created.Data.Id, sku.Id)).Success);
        Assert.False((await fixture.Service.CreateProductVariantAsync(created.Data.Id,
            new(created.Data.Id, null, null, "EXTRA", 10m, SellingPrice: 30m))).Success);
        var edit = await fixture.Service.UpdateProductVariantAsync(created.Data.Id, sku.Id,
            new(null, null, sku.Barcode, 15m, SellingPrice: 27m));
        Assert.True(edit.Success, edit.Error);
        Assert.Equal(27m, (await fixture.Service.GetProductAsync(created.Data.Id)).Data!.SellingPrice);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RuntimeValidationRequiresParentPriceOnlyForSimpleProducts(bool hasVariants)
    {
        var request = Request(null) with { HasVariants = hasVariants };
        Assert.Equal(hasVariants,
            new CreateProductRequestValidator(new CatalogPriceCalculator()).Validate(request).IsValid);
    }

    [Fact]
    public async Task EmptyVariantParentCanBecomeSimpleAndReceivesItsSingleSku()
    {
        using var fixture = new Fixture(postgres);
        var created = await fixture.Service.CreateProductAsync(Request(null));
        var updated = await fixture.Service.UpdateProductAsync(created.Data!.Id,
            Request(25m) with { HasVariants = false });
        Assert.True(updated.Success, updated.Error);
        Assert.False(updated.Data!.HasVariants);
        Assert.Equal(25m, updated.Data.SellingPrice);
        Assert.Equal(25m, Assert.Single(updated.Data.Variants).SellingPrice);
    }

    [Fact]
    public async Task LegacyMissingPricesRemainEditableButCannotBeUsedForSales()
    {
        using var fixture = new Fixture(postgres);
        var product = new Product(Guid.NewGuid(), "S", "P", "Test", 22m, 10m);
        var variant = new ProductVariant(product.Id, "OLD", 10m, product.SupplierId) { Product = product };
        fixture.Db.AddRange(product, variant);
        await fixture.Db.SaveChangesAsync();
        var catalog = await fixture.Service.GetProductAsync(product.Id);
        Assert.True(catalog.Success, catalog.Error);
        Assert.Null(Assert.Single(catalog.Data!.Variants).SellingPrice);
        var barcode = await fixture.Service.GetVariantByBarcodeAsync("OLD");
        Assert.False(barcode.Success);
        Assert.Contains("mancante", barcode.Error);
        variant.SellingPrice = -1m;
        Assert.False(ProductMapper.MapToDto(product, new CatalogPriceCalculator()).Success);
    }

    private static CreateProductRequest Request(decimal? price) =>
        new(Guid.NewGuid(), "SUP", "PRODUCT", "Articolo", price, 22m, 10m);

    private sealed class Fixture : IDisposable
    {
        public MauwDbContext Db { get; }
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public ProductService Service { get; }
        public Fixture(PostgreSqlContainerFixture postgres)
        {
            Db = postgres.CreateDbContext();
            Service = new(new UnitOfWork(Db, _cache), new CreateProductRequestValidator(new()), new(),
                new ImageApplicationService(new ProductVariantImageRepository(Db),
                new Moq.Mock<IImageStorageService>().Object,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageApplicationService>.Instance));
        }
        public void Dispose() { Db.Dispose(); _cache.Dispose(); }
    }
}
