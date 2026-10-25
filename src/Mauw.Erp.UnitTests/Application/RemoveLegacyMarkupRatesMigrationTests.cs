using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class RemoveLegacyMarkupRatesMigrationTests(PostgreSqlContainerFixture postgres)
{
    private const string PreviousMigration = "20261025141209_AddPriceFormula";
    private const string TargetMigration = "20261025151054_RemoveLegacyMarkupRates";

    [Fact]
    public async Task ModelSchemaHasNoLegacyMarkupColumnsAndProductAndVariantRoundTrip()
    {
        await using var db = postgres.CreateDbContext();
        Assert.Empty(await MarkupColumnsAsync(db));

        var code = $"MK-{Guid.NewGuid():N}";
        var product = new Product(Guid.NewGuid(), code, code, "Articolo senza ricarico", 22m, 10.50m)
            { SellingPrice = 23.12345678m };
        var variant = new ProductVariant(product.Id, code, 10.50m, product.SupplierId)
            { Product = product, SellingPrice = 17.12345678m };
        db.AddRange(product, variant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.ProductVariants.Include(item => item.Product).SingleAsync(item => item.Id == variant.Id);
        Assert.Equal(17.12345678m, loaded.SellingPrice);
        Assert.Equal(23.12345678m, loaded.Product.SellingPrice);
        Assert.Equal(10.50m, loaded.Product.BasePurchaseCost);
        Assert.Equal(22m, loaded.Product.VatRate);
    }

    [Fact]
    public async Task MigrationDropsOnlyTheMarkupColumnsKeepsDataAndDownRestoresThem()
    {
        await using var db = postgres.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(["default_markup_rate", "markup_rate"], await MarkupColumnsAsync(db));

        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        await InsertLegacyRowsAsync(db, productId, variantId);
        var before = await SnapshotAsync(db, productId, variantId);
        Assert.Equal(40.00m, before.DefaultMarkupRate);
        Assert.Equal(55.50m, before.MarkupRate);

        await migrator.MigrateAsync(TargetMigration);
        Assert.Empty(await MarkupColumnsAsync(db));
        var after = await SnapshotAsync(db, productId, variantId);
        Assert.Equal(before with { DefaultMarkupRate = null, MarkupRate = null }, after);
        var loaded = await db.ProductVariants.Include(item => item.Product).SingleAsync(item => item.Id == variantId);
        Assert.Equal(17.12345678m, loaded.SellingPrice);
        Assert.Equal(23.12345678m, loaded.Product.SellingPrice);

        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(["default_markup_rate", "markup_rate"], await MarkupColumnsAsync(db));
        var columns = await ColumnDefinitionsAsync(db);
        var productColumn = columns["default_markup_rate"];
        Assert.Equal(("numeric", 6, 2, "NO"), (productColumn.Type, productColumn.Precision, productColumn.Scale, productColumn.Nullable));
        Assert.Equal(0m, decimal.Parse(productColumn.Default!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(("numeric", 6, 2, "YES", null), columns["markup_rate"]);
        var restored = await SnapshotAsync(db, productId, variantId);
        Assert.Equal(0m, restored.DefaultMarkupRate);
        Assert.Null(restored.MarkupRate);
        Assert.Equal(before with { DefaultMarkupRate = 0m, MarkupRate = null }, restored);
    }

    private sealed record Snapshot(
        decimal? DefaultMarkupRate, decimal? MarkupRate, decimal SellingPrice,
        decimal ProductSellingPrice, decimal VariantPurchaseCost, decimal BasePurchaseCost);

    private static async Task<NpgsqlConnection> OpenAsync(DbContext db)
    {
        var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<List<string>> MarkupColumnsAsync(DbContext db)
    {
        await using var connection = await OpenAsync(db);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name IN ('products', 'product_variants')
              AND column_name IN ('default_markup_rate', 'markup_rate') ORDER BY column_name
            """;
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }

    private static async Task<Dictionary<string, (string Type, int Precision, int Scale, string Nullable, string? Default)>>
        ColumnDefinitionsAsync(DbContext db)
    {
        await using var connection = await OpenAsync(db);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_name, data_type, numeric_precision, numeric_scale, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name IN ('products', 'product_variants')
              AND column_name IN ('default_markup_rate', 'markup_rate')
            """;
        var result = new Dictionary<string, (string, int, int, string, string?)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = (reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5));
        return result;
    }

    private static async Task InsertLegacyRowsAsync(DbContext db, Guid productId, Guid variantId)
    {
        await using var connection = await OpenAsync(db);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO products (id, supplier_id, supplier_code, barcode, name, default_markup_rate, selling_price,
                vat_rate, base_purchase_cost, has_variants, created_at, updated_at)
            VALUES (@product, @supplier, 'SUP', 'LEGACY-P', 'Legacy', 40.00, 23.12345678, 22.00, 10.50, true, now(), now());
            INSERT INTO product_variants (id, product_id, supplier_id, barcode, purchase_cost, selling_price,
                markup_rate, created_at, updated_at)
            VALUES (@variant, @product, @supplier, 'LEGACY-V', 10.50, 17.12345678, 55.50, now(), now());
            """;
        command.Parameters.AddWithValue("product", productId);
        command.Parameters.AddWithValue("variant", variantId);
        command.Parameters.AddWithValue("supplier", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Snapshot> SnapshotAsync(DbContext db, Guid productId, Guid variantId)
    {
        await using var connection = await OpenAsync(db);
        var columns = await ColumnDefinitionsAsync(db);
        var productMarkup = columns.ContainsKey("default_markup_rate") ? "p.default_markup_rate" : "NULL::numeric";
        var variantMarkup = columns.ContainsKey("markup_rate") ? "v.markup_rate" : "NULL::numeric";
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {productMarkup}, {variantMarkup}, v.selling_price, p.selling_price, v.purchase_cost, p.base_purchase_cost
            FROM product_variants v JOIN products p ON p.id = v.product_id
            WHERE v.id = @variant AND p.id = @product
            """;
        command.Parameters.AddWithValue("product", productId);
        command.Parameters.AddWithValue("variant", variantId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new Snapshot(
            reader.IsDBNull(0) ? null : reader.GetDecimal(0), reader.IsDBNull(1) ? null : reader.GetDecimal(1),
            reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5));
    }
}
