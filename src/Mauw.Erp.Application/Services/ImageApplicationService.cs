using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;

namespace Mauw.Erp.Application.Services;

public sealed class ImageApplicationService(
    IProductVariantImageRepository images,
    IImageStorageService storage) : IImageApplicationService
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public async Task<OperationResult<UploadedImageDto>> UploadAsync(
        Guid targetId, ImageUpload upload, ImageCategory? category, CancellationToken ct = default)
    {
        var error = Validate(upload, category);
        if (error is not null)
            return OperationResult<UploadedImageDto>.Fail(error);

        var target = await images.GetTargetAsync(targetId, ct);
        if (target is null)
            return OperationResult<UploadedImageDto>.Fail(ImageErrors.TargetNotFound);

        // Product with variants -> images go on variants; product without variants -> images go on the product.
        if ((target.Type == ImageTargetType.Product) == target.ProductHasVariants)
            return OperationResult<UploadedImageDto>.Fail(ImageErrors.TargetInconsistent);

        // First photo: Main/0. Next ones: requested category or Zoom1, SortOrder = max + 1.
        var maxSortOrder = await images.GetMaxSortOrderAsync(targetId, ct);
        var (resolvedCategory, sortOrder) = maxSortOrder is null
            ? (category ?? ImageCategory.Main, 0)
            : (category ?? ImageCategory.Zoom1, maxSortOrder.Value + 1);

        // Client file names are never trusted: the key uses a generated name.
        var fileName = $"{Guid.NewGuid():N}{Extensions[upload.ContentType]}";
        var key = await storage.UploadImageAsync(targetId, upload.Content, fileName, upload.ContentType, ct);

        try
        {
            var image = new ProductVariantImage(targetId, key, resolvedCategory, sortOrder);
            await images.AddAsync(image, ct);
            await images.SaveChangesAsync(ct);

            return OperationResult<UploadedImageDto>.Ok(new UploadedImageDto(
                image.Id, targetId, $"/api/{key}", resolvedCategory, sortOrder));
        }
        catch
        {
            await storage.DeleteImageAsync(key, CancellationToken.None);
            throw;
        }
    }

    public async Task<OperationResult<SkuResolutionDto>> ResolveSkuAsync(string sku, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return OperationResult<SkuResolutionDto>.Fail(ImageErrors.SkuNotFound);

        var matches = await images.FindBySkuAsync(sku.Trim(), ct);

        // Images of a product without variants belong to the product, even when found via its single hidden variant.
        // A product with variants cannot be addressed by its own SKU: images belong to the variants.
        var targets = matches
            .Select(m => m switch
            {
                { VariantId: null, ProductHasVariants: true } => new Resolved(null, true),
                { VariantId: { } variantId, ProductHasVariants: true } =>
                    new Resolved(new SkuResolutionDto(variantId, ImageTargetType.Variant), false),
                _ => new Resolved(new SkuResolutionDto(m.ProductId, ImageTargetType.Product), false)
            })
            .Distinct()
            .ToList();

        if (targets.Count == 0)
            return OperationResult<SkuResolutionDto>.Fail(ImageErrors.SkuNotFound);
        if (targets.Count > 1)
            return OperationResult<SkuResolutionDto>.Fail(ImageErrors.SkuAmbiguous);

        return targets[0].Inconsistent
            ? OperationResult<SkuResolutionDto>.Fail(ImageErrors.SkuInconsistent)
            : OperationResult<SkuResolutionDto>.Ok(targets[0].Target!);
    }

    private sealed record Resolved(SkuResolutionDto? Target, bool Inconsistent);

    private static string? Validate(ImageUpload upload, ImageCategory? category)
    {
        if (upload.Length <= 0) return ImageErrors.EmptyFile;
        if (upload.Length > MaxFileBytes) return ImageErrors.FileTooLarge;
        if (!Extensions.ContainsKey(upload.ContentType) || !upload.Content.CanSeek) return ImageErrors.UnsupportedType;
        if (category is not null && !Enum.IsDefined(category.Value)) return ImageErrors.InvalidCategory;

        Span<byte> header = stackalloc byte[12];
        var read = upload.Content.Read(header);
        upload.Content.Position = 0;

        return MatchesSignature(upload.ContentType, header[..read]) ? null : ImageErrors.UnsupportedType;
    }

    private static bool MatchesSignature(string contentType, ReadOnlySpan<byte> h) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            "image/png" => h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
}
