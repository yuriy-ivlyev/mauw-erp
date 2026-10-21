using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

/// <summary>Variante vetrina, miniatura in lista, categoria immagini e pulizia alla cancellazione, su EF InMemory.</summary>
public class ProductGalleryTests : IDisposable
{
    private readonly MauwDbContext _db = new(new DbContextOptionsBuilder<MauwDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Mock<IImageStorageService> _storage = new();
    private readonly ImageApplicationService _images;
    private readonly ProductService _products;

    public ProductGalleryTests()
    {
        var repository = new ProductVariantImageRepository(_db);
        _images = new ImageApplicationService(repository, _storage.Object, NullLogger<ImageApplicationService>.Instance);
        _products = new ProductService(new UnitOfWork(_db, _cache), new CreateProductRequestValidator(), new(), _images);
    }

    public void Dispose() { _db.Dispose(); _cache.Dispose(); }

    private async Task<Product> ProductWithVariantsAsync(int variantCount)
    {
        var product = new Product(Guid.NewGuid(), "S", $"P{Guid.NewGuid():N}"[..10], "Articolo", 0m, 22m, 10m, hasVariants: true);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        for (var i = 0; i < variantCount; i++)
        {
            var variant = new ProductVariant(product.Id, $"B-{Guid.NewGuid():N}"[..12], 10m);
            _db.ProductVariants.Add(variant);
            await _db.SaveChangesAsync();
        }
        return product;
    }

    private async Task<ProductVariantImage> AddImageAsync(Guid targetId, ImageCategory category, int sortOrder)
    {
        var image = new ProductVariantImage(targetId, $"images/{targetId}/{Guid.NewGuid():N}.jpg", category, sortOrder);
        _db.ProductVariantImages.Add(image);
        await _db.SaveChangesAsync();
        return image;
    }

    [Fact]
    public async Task VarianteVetrinaIsUniquePerProductAndCanBeCleared()
    {
        var product = await ProductWithVariantsAsync(3);
        var (a, b, c) = (product.Variants.ElementAt(0), product.Variants.ElementAt(1), product.Variants.ElementAt(2));

        Assert.True((await _products.SetVarianteVetrinaAsync(product.Id, a.Id, true)).Success);
        Assert.True((await _products.SetVarianteVetrinaAsync(product.Id, b.Id, true)).Success);

        var flagged = await _db.ProductVariants.AsNoTracking().Where(v => v.VarianteVetrina).Select(v => v.Id).ToListAsync();
        Assert.Equal([b.Id], flagged);

        Assert.True((await _products.SetVarianteVetrinaAsync(product.Id, b.Id, false)).Success);
        Assert.Empty(await _db.ProductVariants.AsNoTracking().Where(v => v.VarianteVetrina).ToListAsync());
        Assert.NotEqual(Guid.Empty, c.Id);
    }

    [Fact]
    public async Task VarianteVetrinaRejectsSimpleProductsAndUnknownVariants()
    {
        var simple = new Product(Guid.NewGuid(), "S", "SIMPLE", "Semplice", 0m, 22m, 10m, hasVariants: false);
        _db.Products.Add(simple);
        await _db.SaveChangesAsync();
        var withVariants = await ProductWithVariantsAsync(1);

        Assert.False((await _products.SetVarianteVetrinaAsync(simple.Id, Guid.NewGuid(), true)).Success);
        Assert.Equal("Variant not found", (await _products.SetVarianteVetrinaAsync(withVariants.Id, Guid.NewGuid(), true)).Error);
        Assert.Equal("Product not found", (await _products.SetVarianteVetrinaAsync(Guid.NewGuid(), Guid.NewGuid(), true)).Error);
    }

    [Fact]
    public async Task ThumbnailUsesVarianteVetrinaThenFallsBackToOtherVariants()
    {
        var product = await ProductWithVariantsAsync(3);
        var (first, second, third) = (product.Variants.ElementAt(0), product.Variants.ElementAt(1), product.Variants.ElementAt(2));
        var firstMain = await AddImageAsync(first.Id, ImageCategory.Main, 0);
        var thirdMain = await AddImageAsync(third.Id, ImageCategory.Main, 0);

        // No vetrina: first variant that has images
        var fallback = await _images.GetProductThumbnailUrlsAsync([product]);
        Assert.Equal(ImageStorageKeys.ToUrl(firstMain.FilePath), fallback[product.Id]);

        await _products.SetVarianteVetrinaAsync(product.Id, third.Id, true);
        var reloaded = (await _db.Products.Include(p => p.Variants).AsNoTracking().FirstAsync(p => p.Id == product.Id));
        var vetrina = await _images.GetProductThumbnailUrlsAsync([reloaded]);
        Assert.Equal(ImageStorageKeys.ToUrl(thirdMain.FilePath), vetrina[product.Id]);

        // Vetrina without images: falls back instead of showing nothing
        await _products.SetVarianteVetrinaAsync(product.Id, second.Id, true);
        reloaded = await _db.Products.Include(p => p.Variants).AsNoTracking().FirstAsync(p => p.Id == product.Id);
        Assert.Equal(ImageStorageKeys.ToUrl(firstMain.FilePath), (await _images.GetProductThumbnailUrlsAsync([reloaded]))[product.Id]);
    }

    [Fact]
    public async Task ThumbnailOfSimpleProductUsesItsOwnCoverAndIsOmittedWithoutImages()
    {
        var simple = new Product(Guid.NewGuid(), "S", "SIMPLE", "Semplice", 0m, 22m, 10m, hasVariants: false);
        var empty = new Product(Guid.NewGuid(), "S", "EMPTY", "Vuoto", 0m, 22m, 10m, hasVariants: false);
        _db.Products.AddRange(simple, empty);
        await _db.SaveChangesAsync();
        await AddImageAsync(simple.Id, ImageCategory.Zoom1, 1);
        var cover = await AddImageAsync(simple.Id, ImageCategory.Main, 2);

        var thumbs = await _images.GetProductThumbnailUrlsAsync([simple, empty]);

        Assert.Equal(ImageStorageKeys.ToUrl(cover.FilePath), thumbs[simple.Id]);
        Assert.False(thumbs.ContainsKey(empty.Id));
    }

    [Fact]
    public async Task ThumbnailFallsBackToLowestSortOrderWhenNoMainExists()
    {
        var simple = new Product(Guid.NewGuid(), "S", "SIMPLE", "Semplice", 0m, 22m, 10m, hasVariants: false);
        _db.Products.Add(simple);
        await _db.SaveChangesAsync();
        await AddImageAsync(simple.Id, ImageCategory.Left, 5);
        var lowest = await AddImageAsync(simple.Id, ImageCategory.Right, 2);

        Assert.Equal(ImageStorageKeys.ToUrl(lowest.FilePath), (await _images.GetProductThumbnailUrlsAsync([simple]))[simple.Id]);
    }

    [Fact]
    public async Task ListedProductCarriesTheThumbnailOfItsVetrina()
    {
        var product = await ProductWithVariantsAsync(2);
        var vetrina = product.Variants.ElementAt(1);
        var cover = await AddImageAsync(vetrina.Id, ImageCategory.Main, 0);
        await _products.SetVarianteVetrinaAsync(product.Id, vetrina.Id, true);

        var page = await _products.GetProductsAsync(1, 20);

        var dto = Assert.Single(page.Data!.Items);
        Assert.Equal(ImageStorageKeys.ToUrl(cover.FilePath), dto.MainImageUrl);
        Assert.True(dto.Variants.Single(v => v.Id == vetrina.Id).VarianteVetrina);
    }

    [Fact]
    public async Task SettingMainSwapsCategoriesWithTheCurrentMain()
    {
        var product = await ProductWithVariantsAsync(1);
        var target = product.Variants.Single().Id;
        var main = await AddImageAsync(target, ImageCategory.Main, 0);
        var left = await AddImageAsync(target, ImageCategory.Left, 1);
        var zoom = await AddImageAsync(target, ImageCategory.Zoom1, 2);

        var result = await _images.SetCategoryAsync(target, left.Id, ImageCategory.Main);

        Assert.True(result.Success, result.Error);
        var categories = await _db.ProductVariantImages.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Category);
        Assert.Equal(ImageCategory.Main, categories[left.Id]);
        Assert.Equal(ImageCategory.Left, categories[main.Id]);
        Assert.Equal(ImageCategory.Zoom1, categories[zoom.Id]);
    }

    [Fact]
    public async Task ChangingCategoryRejectsInvalidAndUnknownImages()
    {
        var product = await ProductWithVariantsAsync(1);
        var target = product.Variants.Single().Id;
        var image = await AddImageAsync(target, ImageCategory.Zoom1, 0);

        Assert.Equal(ImageErrors.InvalidCategory, (await _images.SetCategoryAsync(target, image.Id, (ImageCategory)99)).Error);
        Assert.Equal(ImageErrors.ImageNotFound, (await _images.SetCategoryAsync(target, Guid.NewGuid(), ImageCategory.Left)).Error);
        Assert.Equal(ImageErrors.ImageNotFound, (await _images.SetCategoryAsync(Guid.NewGuid(), image.Id, ImageCategory.Left)).Error);
    }

    [Fact]
    public async Task DeletingProductRemovesItsImagesAndThoseOfItsVariants()
    {
        var product = await ProductWithVariantsAsync(2);
        var variantImage = await AddImageAsync(product.Variants.First().Id, ImageCategory.Main, 0);

        var result = await _products.DeleteProductAsync(product.Id);

        Assert.True(result.Success, result.Error);
        Assert.Empty(await _db.ProductVariantImages.ToListAsync());
        _storage.Verify(s => s.DeleteImageAsync(variantImage.FilePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletingVariantRemovesOnlyItsImages()
    {
        var product = await ProductWithVariantsAsync(2);
        var (gone, kept) = (product.Variants.ElementAt(0), product.Variants.ElementAt(1));
        await AddImageAsync(gone.Id, ImageCategory.Main, 0);
        var keptImage = await AddImageAsync(kept.Id, ImageCategory.Main, 0);

        var result = await _products.DeleteProductVariantAsync(product.Id, gone.Id);

        Assert.True(result.Success, result.Error);
        Assert.Equal([keptImage.Id], await _db.ProductVariantImages.Select(i => i.Id).ToListAsync());
    }
}
