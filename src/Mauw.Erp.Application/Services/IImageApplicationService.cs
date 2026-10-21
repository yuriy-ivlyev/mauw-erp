using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Application.Services;

/// <summary>Content of an uploaded image; the stream must be seekable.</summary>
public sealed record ImageUpload(Stream Content, long Length, string ContentType);

public static class ImageErrors
{
    public const string EmptyFile = "File is empty.";
    public const string FileTooLarge = "File exceeds the maximum allowed size.";
    public const string UnsupportedType = "Only JPEG, PNG and WebP images are allowed.";
    public const string InvalidCategory = "Invalid image category.";
    public const string TargetInconsistent =
        "Images of a product with variants belong to its variants; images of a product without variants belong to the product.";
    public const string TargetNotFound = "Target not found.";
    public const string ImageNotFound = "Image not found.";
    public const string MainAlreadyExists =
        "A Main image already exists for this target: change the category of the existing one to Main instead.";
    public const string SkuNotFound = "SKU not found.";
    public const string SkuAmbiguous = "SKU matches more than one target.";
    public const string SkuInconsistent = "SKU belongs to a product with variants: use the SKU of a specific variant.";
}

public interface IImageApplicationService
{
    Task<OperationResult<ImageDto>> UploadAsync(
        Guid targetId, ImageUpload upload, ImageCategory? category, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<ImageDto>>> ListAsync(Guid targetId, CancellationToken ct = default);

    Task<OperationResult<bool>> DeleteAsync(Guid targetId, Guid imageId, CancellationToken ct = default);

    /// <summary>
    /// Changes the category of an image. Setting Main swaps categories with the current Main image,
    /// so a target never has two Main images.
    /// </summary>
    Task<OperationResult<ImageDto>> SetCategoryAsync(
        Guid targetId, Guid imageId, ImageCategory category, CancellationToken ct = default);

    /// <summary>Removes every image of the given targets (records and stored objects), e.g. when a product is deleted.</summary>
    Task DeleteAllAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct = default);

    /// <summary>
    /// List thumbnail URL per product id: the cover of the product itself, or of its varianteVetrina
    /// (falling back to the other variants) when it has variants. Products without images are omitted.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetProductThumbnailUrlsAsync(
        IReadOnlyCollection<Product> products, CancellationToken ct = default);

    Task<OperationResult<SkuResolutionDto>> ResolveSkuAsync(string sku, CancellationToken ct = default);
}
