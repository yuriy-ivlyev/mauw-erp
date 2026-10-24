using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class ProductSupplierCodeConstraintTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public void ProductAndVariantSupplierCodePairsHaveUniqueIndexes()
    {
        using var db = postgres.CreateDbContext();

        AssertUniqueIndex<Product>(db, nameof(Product.SupplierId), nameof(Product.SupplierCode));
        AssertUniqueIndex<ProductVariant>(db, nameof(ProductVariant.SupplierId), nameof(ProductVariant.SupplierCode));
    }

    private static void AssertUniqueIndex<TEntity>(MauwDbContext db, params string[] propertyNames)
        where TEntity : class
    {
        var entity = db.Model.FindEntityType(typeof(TEntity))!;
        var index = Assert.Single(entity.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
        Assert.True(index.IsUnique);
    }
}
