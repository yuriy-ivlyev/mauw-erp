using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class ProductVariantImageRepositoryTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task FindBySkuReturnsVariantAndProductMatchesWithHasVariantsFlag()
    {
        using var db = postgres.CreateDbContext();
        var supplier = Guid.NewGuid();
        var parent = new Product(supplier, "S1", "PARENT", "Con varianti", 0m, 22m, 10m, hasVariants: true);
        var simple = new Product(supplier, "S2", "SIMPLE", "Semplice", 0m, 22m, 10m, hasVariants: false);
        db.Products.AddRange(parent, simple);
        await db.SaveChangesAsync();
        var variant = new ProductVariant(parent.Id, "VAR-1", 10m, parent.SupplierId);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        var repo = new ProductVariantImageRepository(db);

        var byVariant = Assert.Single(await repo.FindBySkuAsync("VAR-1", default));
        var byParent = Assert.Single(await repo.FindBySkuAsync("PARENT", default));
        var bySimple = Assert.Single(await repo.FindBySkuAsync("SIMPLE", default));

        Assert.Equal(new(parent.Id, variant.Id, true), byVariant);
        Assert.Equal(new(parent.Id, null, true), byParent);
        Assert.Equal(new(simple.Id, null, false), bySimple);
        Assert.Empty(await repo.FindBySkuAsync("missing", default));

        Assert.Equal(new ImageTargetInfo(ImageTargetType.Variant, true), await repo.GetTargetAsync(variant.Id, default));
        Assert.Equal(new ImageTargetInfo(ImageTargetType.Product, true), await repo.GetTargetAsync(parent.Id, default));
        Assert.Equal(new ImageTargetInfo(ImageTargetType.Product, false), await repo.GetTargetAsync(simple.Id, default));
        Assert.Null(await repo.GetTargetAsync(Guid.NewGuid(), default));
    }
}
