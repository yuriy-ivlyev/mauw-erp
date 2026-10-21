using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Services;

public sealed class ImageApplicationService(
    IProductVariantImageRepository images,
    IImageStorageService storage,
    ILogger<ImageApplicationService> logger) : IImageApplicationService
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private static ImageDto ToDto(ProductVariantImage i) =>
        new(i.Id, i.TargetId, ImageStorageKeys.ToUrl(i.FilePath), i.Category, i.SortOrder);

    public async Task<OperationResult<ImageDto>> SetCategoryAsync(
        Guid targetId, Guid imageId, ImageCategory category, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(category))
            return OperationResult<ImageDto>.Fail(ImageErrors.InvalidCategory);

        var image = await images.GetAsync(targetId, imageId, ct);
        if (image is null)
            return OperationResult<ImageDto>.Fail(ImageErrors.ImageNotFound);

        if (image.Category != category)
        {
            if (category == ImageCategory.Main)
            {
                // The current Main takes the category this image leaves, so no category is lost.
                var currentMain = await images.GetMainAsync(targetId, ct);
                currentMain?.ChangeCategory(image.Category);
            }

            image.ChangeCategory(category);
            await images.SaveChangesAsync(ct);
        }

        return OperationResult<ImageDto>.Ok(ToDto(image));
    }

    public async Task DeleteAllAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct = default)
    {
        if (targetIds.Count == 0)
            return;

        var all = await images.GetByTargetsAsync(targetIds, ct);
        if (all.Count == 0)
            return;

        foreach (var image in all)
            images.Remove(image);
        await images.SaveChangesAsync(ct);

        foreach (var image in all)
        {
            try
            {
                await storage.DeleteImageAsync(image.FilePath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image {FilePath} removed from the database but not from storage", image.FilePath);
            }
        }
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetProductThumbnailUrlsAsync(
        IReadOnlyCollection<Product> products, CancellationToken ct = default)
    {
        // Candidate targets per product, best first: varianteVetrina, then the other variants by age.
        var candidates = products.ToDictionary(
            p => p.Id,
            p => p.HasVariants
                ? p.Variants.OrderByDescending(v => v.VarianteVetrina).ThenBy(v => v.CreatedAt).Select(v => v.Id).ToList()
                : [p.Id]);

        var covers = (await images.GetCoversAsync(candidates.Values.SelectMany(ids => ids).ToList(), ct))
            .ToDictionary(i => i.TargetId);

        var result = new Dictionary<Guid, string>();
        foreach (var (productId, ids) in candidates)
        {
            var cover = ids.Where(covers.ContainsKey).Select(id => covers[id]).FirstOrDefault();
            if (cover is not null)
                result[productId] = ImageStorageKeys.ToUrl(cover.FilePath);
        }

        return result;
    }

    public async Task<OperationResult<IReadOnlyList<ImageDto>>> ListAsync(Guid targetId, CancellationToken ct = default)
    {
        if (await images.GetTargetAsync(targetId, ct) is null)
            return OperationResult<IReadOnlyList<ImageDto>>.Fail(ImageErrors.TargetNotFound);

        var list = await images.GetByTargetAsync(targetId, ct);
        return OperationResult<IReadOnlyList<ImageDto>>.Ok(list.Select(ToDto).ToList());
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid targetId, Guid imageId, CancellationToken ct = default)
    {
        var image = await images.GetAsync(targetId, imageId, ct);
        if (image is null)
            return OperationResult<bool>.Fail(ImageErrors.ImageNotFound);

        images.Remove(image);
        await images.SaveChangesAsync(ct);

        // The record is already gone: a failed object delete only leaves an orphan, it must not fail the request.
        try
        {
            await storage.DeleteImageAsync(image.FilePath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Image {FilePath} removed from the database but not from storage", image.FilePath);
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<ImageDto>> UploadAsync(
        Guid targetId, ImageUpload upload, ImageCategory? category, CancellationToken ct = default)
    {
        var error = Validate(upload, category);
        if (error is not null)
            return OperationResult<ImageDto>.Fail(error);

        var target = await images.GetTargetAsync(targetId, ct);
        if (target is null)
            return OperationResult<ImageDto>.Fail(ImageErrors.TargetNotFound);

        // Product with variants -> images go on variants; product without variants -> images go on the product.
        if ((target.Type == ImageTargetType.Product) == target.ProductHasVariants)
            return OperationResult<ImageDto>.Fail(ImageErrors.TargetInconsistent);

        // First photo: Main/0. Next ones: requested category or Zoom1, SortOrder = max + 1.
        var maxSortOrder = await images.GetMaxSortOrderAsync(targetId, ct);
        if (category == ImageCategory.Main && maxSortOrder is not null && await images.GetMainAsync(targetId, ct) is not null)
            return OperationResult<ImageDto>.Fail(ImageErrors.MainAlreadyExists);
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

            return OperationResult<ImageDto>.Ok(ToDto(image));
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
