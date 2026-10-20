using Mauw.Erp.Contracts.Dtos;
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
    public const string SkuNotFound = "SKU not found.";
    public const string SkuAmbiguous = "SKU matches more than one target.";
    public const string SkuInconsistent = "SKU belongs to a product with variants: use the SKU of a specific variant.";
}

public interface IImageApplicationService
{
    Task<OperationResult<UploadedImageDto>> UploadAsync(
        Guid targetId, ImageUpload upload, ImageCategory? category, CancellationToken ct = default);

    Task<OperationResult<SkuResolutionDto>> ResolveSkuAsync(string sku, CancellationToken ct = default);
}
