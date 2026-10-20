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
    Task<int?> GetMaxSortOrderAsync(Guid targetId, CancellationToken ct);
    Task AddAsync(ProductVariantImage image, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);

    Task<ImageTargetInfo?> GetTargetAsync(Guid targetId, CancellationToken ct);

    /// <summary>Returns the variants (by barcode) and products (by InternalCode) matching the SKU.</summary>
    Task<IReadOnlyList<SkuMatch>> FindBySkuAsync(string sku, CancellationToken ct);
}
