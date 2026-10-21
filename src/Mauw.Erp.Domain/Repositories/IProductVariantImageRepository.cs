using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Raw SKU match. VariantId is null when the SKU is the product's own code (InternalCode),
/// set when it is a variant barcode.
/// </summary>
public sealed record SkuMatch(Guid ProductId, Guid? VariantId, bool ProductHasVariants);

/// <summary>Product or variant addressed by an image TargetId; ProductHasVariants refers to the owning product.</summary>
public sealed record ImageTargetInfo(ImageTargetType Type, bool ProductHasVariants);

public interface IProductVariantImageRepository
{
    Task<IReadOnlyList<ProductVariantImage>> GetByTargetAsync(Guid targetId, CancellationToken ct);
    Task<ProductVariantImage?> GetAsync(Guid targetId, Guid imageId, CancellationToken ct);

    /// <summary>The image currently marked Main for the target (tracked), if any.</summary>
    Task<ProductVariantImage?> GetMainAsync(Guid targetId, CancellationToken ct);

    /// <summary>All images of the given targets (tracked).</summary>
    Task<IReadOnlyList<ProductVariantImage>> GetByTargetsAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct);

    /// <summary>One cover image per target that has images: the Main one, otherwise the lowest SortOrder.</summary>
    Task<IReadOnlyList<ProductVariantImage>> GetCoversAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct);
    void Remove(ProductVariantImage image);
    Task<int?> GetMaxSortOrderAsync(Guid targetId, CancellationToken ct);
    Task AddAsync(ProductVariantImage image, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);

    Task<ImageTargetInfo?> GetTargetAsync(Guid targetId, CancellationToken ct);

    /// <summary>Returns the variants (by barcode) and products (by InternalCode) matching the SKU.</summary>
    Task<IReadOnlyList<SkuMatch>> FindBySkuAsync(string sku, CancellationToken ct);
}
