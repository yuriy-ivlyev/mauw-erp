using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public sealed class ProductVariantImageRepository(DbContext.MauwDbContext db) : IProductVariantImageRepository
{
    public async Task<IReadOnlyList<ProductVariantImage>> GetByTargetAsync(Guid targetId, CancellationToken ct) =>
        await db.ProductVariantImages.AsNoTracking()
            .Where(i => i.TargetId == targetId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);

    public async Task<ProductVariantImage?> GetAsync(Guid targetId, Guid imageId, CancellationToken ct) =>
        await db.ProductVariantImages.FirstOrDefaultAsync(i => i.Id == imageId && i.TargetId == targetId, ct);

    public void Remove(ProductVariantImage image) => db.ProductVariantImages.Remove(image);

    public async Task<ProductVariantImage?> GetMainAsync(Guid targetId, CancellationToken ct) =>
        await db.ProductVariantImages
            .FirstOrDefaultAsync(i => i.TargetId == targetId && i.Category == ImageCategory.Main, ct);

    public async Task<IReadOnlyList<ProductVariantImage>> GetByTargetsAsync(
        IReadOnlyCollection<Guid> targetIds, CancellationToken ct) =>
        await db.ProductVariantImages.Where(i => targetIds.Contains(i.TargetId)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductVariantImage>> GetCoversAsync(
        IReadOnlyCollection<Guid> targetIds, CancellationToken ct)
    {
        var images = await db.ProductVariantImages.AsNoTracking()
            .Where(i => targetIds.Contains(i.TargetId))
            .ToListAsync(ct);

        return images
            .GroupBy(i => i.TargetId)
            .Select(g => g.OrderBy(i => i.Category == ImageCategory.Main ? 0 : 1).ThenBy(i => i.SortOrder).First())
            .ToList();
    }

    public async Task<int?> GetMaxSortOrderAsync(Guid targetId, CancellationToken ct) =>
        await db.ProductVariantImages
            .Where(i => i.TargetId == targetId)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(ct);

    public async Task AddAsync(ProductVariantImage image, CancellationToken ct) =>
        await db.ProductVariantImages.AddAsync(image, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<ImageTargetInfo?> GetTargetAsync(Guid targetId, CancellationToken ct) =>
        await db.ProductVariants.AsNoTracking()
            .Where(v => v.Id == targetId)
            .Select(v => new ImageTargetInfo(ImageTargetType.Variant, v.Product.HasVariants))
            .FirstOrDefaultAsync(ct)
        ?? await db.Products.AsNoTracking()
            .Where(p => p.Id == targetId)
            .Select(p => new ImageTargetInfo(ImageTargetType.Product, p.HasVariants))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<SkuMatch>> FindBySkuAsync(string sku, CancellationToken ct)
    {
        var variants = await db.ProductVariants.AsNoTracking()
            .Where(v => v.Barcode == sku)
            .Select(v => new SkuMatch(v.ProductId, v.Id, v.Product.HasVariants))
            .Take(2)
            .ToListAsync(ct);

        var products = await db.Products.AsNoTracking()
            .Where(p => p.InternalCode == sku)
            .Select(p => new SkuMatch(p.Id, null, p.HasVariants))
            .Take(2)
            .ToListAsync(ct);

        return [.. variants, .. products];
    }
}
