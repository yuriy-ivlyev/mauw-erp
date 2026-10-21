using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mauw.Erp.UnitTests.Application;

public class ImageApplicationServiceTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly Guid TargetId = Guid.NewGuid();

    private readonly Mock<IProductVariantImageRepository> _repo = new();
    private readonly Mock<IImageStorageService> _storage = new();
    private readonly ImageApplicationService _service;

    public ImageApplicationServiceTests()
    {
        _repo.Setup(r => r.GetTargetAsync(TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageTargetInfo(ImageTargetType.Variant, true));
        _storage.Setup(s => s.UploadImageAsync(TargetId, It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Stream _, string name, string _, CancellationToken _) => $"images/{id}/{name}");
        _service = new ImageApplicationService(_repo.Object, _storage.Object, NullLogger<ImageApplicationService>.Instance);
    }

    private static ImageUpload Upload(byte[]? bytes = null, string contentType = "image/jpeg")
    {
        bytes ??= Jpeg;
        return new ImageUpload(new MemoryStream(bytes), bytes.Length, contentType);
    }

    [Fact]
    public async Task FirstImageWithoutCategoryIsMainWithSortOrderZero()
    {
        _repo.Setup(r => r.GetMaxSortOrderAsync(TargetId, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        var result = await _service.UploadAsync(TargetId, Upload(), null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(ImageCategory.Main, result.Data!.Category);
        Assert.Equal(0, result.Data.SortOrder);
    }

    [Fact]
    public async Task NextImageWithoutCategoryIsZoom1WithMaxPlusOne()
    {
        _repo.Setup(r => r.GetMaxSortOrderAsync(TargetId, It.IsAny<CancellationToken>())).ReturnsAsync(4);

        var result = await _service.UploadAsync(TargetId, Upload(), null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(ImageCategory.Zoom1, result.Data!.Category);
        Assert.Equal(5, result.Data.SortOrder);
    }

    [Fact]
    public async Task ExplicitCategoryIsKept()
    {
        _repo.Setup(r => r.GetMaxSortOrderAsync(TargetId, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _service.UploadAsync(TargetId, Upload(), ImageCategory.OnPerson);

        Assert.Equal(ImageCategory.OnPerson, result.Data!.Category);
        Assert.Equal(1, result.Data.SortOrder);
    }

    [Fact]
    public async Task InvalidInputIsRejectedBeforeTouchingStorage()
    {
        var notAnImage = Upload([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
        var wrongType = Upload(contentType: "text/html");

        Assert.Equal(ImageErrors.UnsupportedType, (await _service.UploadAsync(TargetId, notAnImage, null)).Error);
        Assert.Equal(ImageErrors.UnsupportedType, (await _service.UploadAsync(TargetId, wrongType, null)).Error);
        Assert.Equal(ImageErrors.InvalidCategory, (await _service.UploadAsync(TargetId, Upload(), (ImageCategory)99)).Error);
        _storage.Verify(s => s.UploadImageAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ImageTargetType.Product, true)]   // product with variants addressed directly
    [InlineData(ImageTargetType.Variant, false)]  // hidden variant of a product without variants
    public async Task UploadToInconsistentTargetIsRejectedBeforeTouchingStorage(ImageTargetType type, bool productHasVariants)
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetTargetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageTargetInfo(type, productHasVariants));

        var result = await _service.UploadAsync(id, Upload(), null);

        Assert.Equal(ImageErrors.TargetInconsistent, result.Error);
        _storage.Verify(s => s.UploadImageAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadToProductWithoutVariantsIsAccepted()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetTargetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageTargetInfo(ImageTargetType.Product, false));
        _storage.Setup(s => s.UploadImageAsync(id, It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("images/key");

        var result = await _service.UploadAsync(id, Upload(), null);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task ListReturnsImagesWithProxyUrlsAndReportsUnknownTarget()
    {
        var image = new ProductVariantImage(TargetId, $"images/{TargetId}/a.jpg", ImageCategory.Main, 0);
        _repo.Setup(r => r.GetByTargetAsync(TargetId, It.IsAny<CancellationToken>())).ReturnsAsync([image]);

        var listed = await _service.ListAsync(TargetId);
        var unknown = await _service.ListAsync(Guid.NewGuid());

        var dto = Assert.Single(listed.Data!);
        Assert.Equal($"/api/images/{TargetId}/a.jpg", dto.Url);
        Assert.Equal(ImageCategory.Main, dto.Category);
        Assert.Equal(ImageErrors.TargetNotFound, unknown.Error);
    }

    [Fact]
    public async Task DeleteRemovesRecordThenObjectAndToleratesStorageFailure()
    {
        var image = new ProductVariantImage(TargetId, $"images/{TargetId}/a.jpg", ImageCategory.Zoom1, 1);
        _repo.Setup(r => r.GetAsync(TargetId, image.Id, It.IsAny<CancellationToken>())).ReturnsAsync(image);
        _storage.Setup(s => s.DeleteImageAsync(image.FilePath, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());

        var result = await _service.DeleteAsync(TargetId, image.Id);

        Assert.True(result.Success, result.Error);
        _repo.Verify(r => r.Remove(image), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.DeleteImageAsync(image.FilePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteOfUnknownImageIsNotFoundAndTouchesNothing()
    {
        var result = await _service.DeleteAsync(TargetId, Guid.NewGuid());

        Assert.Equal(ImageErrors.ImageNotFound, result.Error);
        _repo.Verify(r => r.Remove(It.IsAny<ProductVariantImage>()), Times.Never);
        _storage.Verify(s => s.DeleteImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExplicitMainIsRejectedWhenTargetAlreadyHasOne()
    {
        _repo.Setup(r => r.GetMaxSortOrderAsync(TargetId, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _repo.Setup(r => r.GetMainAsync(TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductVariantImage(TargetId, "images/x", ImageCategory.Main, 0));

        var result = await _service.UploadAsync(TargetId, Upload(), ImageCategory.Main);

        Assert.Equal(ImageErrors.MainAlreadyExists, result.Error);
        _storage.Verify(s => s.UploadImageAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnknownTargetIsReported()
    {
        var result = await _service.UploadAsync(Guid.NewGuid(), Upload(), null);

        Assert.Equal(ImageErrors.TargetNotFound, result.Error);
    }

    [Fact]
    public async Task StoredObjectIsDeletedWhenDatabaseSaveFails()
    {
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UploadAsync(TargetId, Upload(), null));

        _storage.Verify(s => s.DeleteImageAsync(It.Is<string>(k => k.StartsWith($"images/{TargetId}/")), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupSku(string sku, params SkuMatch[] matches) =>
        _repo.Setup(r => r.FindBySkuAsync(sku, It.IsAny<CancellationToken>())).ReturnsAsync(matches);

    [Fact]
    public async Task ResolveSkuUnknownReturnsNotFound()
    {
        SetupSku("none");

        Assert.Equal(ImageErrors.SkuNotFound, (await _service.ResolveSkuAsync("none")).Error);
        Assert.Equal(ImageErrors.SkuNotFound, (await _service.ResolveSkuAsync("  ")).Error);
    }

    [Fact]
    public async Task ResolveSkuOfVariantOfProductWithVariantsReturnsVariant()
    {
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        SetupSku("v1", new SkuMatch(productId, variantId, true));

        var result = await _service.ResolveSkuAsync(" v1 ");

        Assert.Equal(new SkuResolutionDto(variantId, ImageTargetType.Variant), result.Data);
    }

    [Fact]
    public async Task ResolveSkuOfProductWithoutVariantsReturnsProduct()
    {
        var productId = Guid.NewGuid();
        SetupSku("simple", new SkuMatch(productId, null, false));

        var result = await _service.ResolveSkuAsync("simple");

        Assert.Equal(new SkuResolutionDto(productId, ImageTargetType.Product), result.Data);
    }

    [Fact]
    public async Task ResolveSkuOfHiddenVariantOfProductWithoutVariantsReturnsProductOnce()
    {
        var productId = Guid.NewGuid();
        SetupSku("MNVR-X", new SkuMatch(productId, Guid.NewGuid(), false), new SkuMatch(productId, null, false));

        var result = await _service.ResolveSkuAsync("MNVR-X");

        Assert.Equal(new SkuResolutionDto(productId, ImageTargetType.Product), result.Data);
    }

    [Fact]
    public async Task ResolveSkuOfProductWithVariantsIsInconsistent()
    {
        SetupSku("parent", new SkuMatch(Guid.NewGuid(), null, true));

        var result = await _service.ResolveSkuAsync("parent");

        Assert.False(result.Success);
        Assert.Equal(ImageErrors.SkuInconsistent, result.Error);
    }

    [Fact]
    public async Task ResolveSkuMatchingDifferentTargetsIsAmbiguous()
    {
        SetupSku("dup",
            new SkuMatch(Guid.NewGuid(), Guid.NewGuid(), true),
            new SkuMatch(Guid.NewGuid(), null, false));

        Assert.Equal(ImageErrors.SkuAmbiguous, (await _service.ResolveSkuAsync("dup")).Error);
    }

    [Theory]
    [InlineData("../x.jpg")]
    [InlineData("a/b.jpg")]
    [InlineData("..")]
    [InlineData("")]
    public void UnsafeFileNamesAreRejected(string name) =>
        Assert.False(ImageStorageKeys.IsValidFileName(name));
}
