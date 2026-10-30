using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class InitialCreateBaselineMigrationTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task BaselineIsFollowedByVariantForeignKeysAndMatchesTheModel()
    {
        await using var db = await MigratedDbAsync();

        var migrations = db.Database.GetMigrations().ToList();
        Assert.Equal(12, migrations.Count);
        Assert.EndsWith("_InitialCreate", migrations[0]);
        Assert.EndsWith("_AddVariantForeignKeys", migrations[1]);
        Assert.EndsWith("_EnforceWholeMerchandiseQuantities", migrations[2]);
        Assert.EndsWith("_AddOrderFulfillmentAndOutbox", migrations[3]);
        Assert.EndsWith("_RemoveOrderOutboxAndAddPaymentInfo", migrations[4]);
        Assert.EndsWith("_AddOrderInternalNotes", migrations[5]);
        Assert.EndsWith("_AddOrderShippingCharge", migrations[6]);
        Assert.EndsWith("_AddShipmentFulfillment", migrations[7]);
        Assert.EndsWith("_AlterInvoiceItemQuantityToDecimal", migrations[8]);
        Assert.EndsWith("_AddInvoicePaymentMetadata", migrations[9]);
        Assert.EndsWith("_AddInvoiceConfirmationWorkflow", migrations[10]);
        Assert.EndsWith("_AddInvoiceOrderSynchronization", migrations[11]);
        Assert.Equal(migrations, await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task OrderOutboxIsDroppedAndCashPaymentFieldsAreAvailable()
    {
        await using var db = await MigratedDbAsync();

        var outbox = await QueryAsync(db, """
            SELECT table_name, table_name FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'outbox_messages'
            """);
        Assert.Empty(outbox);

        var paymentColumns = await QueryAsync(db, """
            SELECT column_name, data_type FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'orders'
              AND column_name IN ('payment_method', 'payment_transaction_details')
            """);
        Assert.Equal("character varying", paymentColumns["payment_method"]);
        Assert.Equal("character varying", paymentColumns["payment_transaction_details"]);

        var invoicePaymentColumns = await QueryAsync(db, """
            SELECT column_name, is_nullable FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'payments'
              AND column_name IN ('payment_method', 'transaction_details')
            """);
        Assert.Equal("YES", invoicePaymentColumns["payment_method"]);
        Assert.Equal("YES", invoicePaymentColumns["transaction_details"]);

        var confirmationColumn = await QueryAsync(db, """
            SELECT column_name, is_nullable FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'invoices'
              AND column_name = 'confirmed_at'
            """);
        Assert.Equal("YES", confirmationColumn["confirmed_at"]);

        var invoiceSyncColumns = await QueryAsync(db, """
            SELECT table_name || '.' || column_name, is_nullable FROM information_schema.columns
            WHERE table_schema = 'public'
              AND ((table_name = 'invoices' AND column_name IN
                    ('sdi_submitted_at', 'shipping_cost', 'shipping_vat_rate', 'shipping_nature_code'))
                OR (table_name = 'order_items' AND column_name IN
                    ('is_billing_only', 'is_excluded_from_invoice')))
            """);
        Assert.Equal("YES", invoiceSyncColumns["invoices.sdi_submitted_at"]);
        Assert.Equal("NO", invoiceSyncColumns["invoices.shipping_cost"]);
        Assert.Equal("YES", invoiceSyncColumns["invoices.shipping_vat_rate"]);
        Assert.Equal("YES", invoiceSyncColumns["invoices.shipping_nature_code"]);
        Assert.Equal("NO", invoiceSyncColumns["order_items.is_billing_only"]);
        Assert.Equal("NO", invoiceSyncColumns["order_items.is_excluded_from_invoice"]);
    }

    [Fact]
    public async Task BaselineCreatesTrigramAndCoveringIndexesAndCheckConstraints()
    {
        await using var db = await MigratedDbAsync();

        var indexes = await QueryAsync(db, "SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'public'");
        foreach (var (name, table, column) in new[]
                 {
                     ("i_x_products_name_trgm", "products", "name"),
                     ("i_x_products_supplier_code_trgm", "products", "supplier_code"),
                     ("i_x_product_variants_supplier_code_trgm", "product_variants", "supplier_code"),
                     ("i_x_product_variants_barcode_trgm", "product_variants", "barcode")
                 })
            Assert.Contains($"ON public.{table} USING gin ({column} gin_trgm_ops)", indexes[name]);

        var covering = indexes["i_x_stock_movements_cost_warehouse_variant_date"];
        Assert.Contains("INCLUDE (quantity_delta, quantity, type, unit_cost)", covering);
        Assert.Contains("WHERE", covering);

        var checks = await QueryAsync(db,
            "SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint WHERE contype = 'c' AND connamespace = 'public'::regnamespace");
        Assert.Equal(
            [
                "ck_price_formula_steps_percentage",
                "ck_price_formula_steps_position",
                "ck_price_formulas_is_singleton",
                "ck_price_list_rules_whole_minimum_quantity",
                "ck_stock_items_whole_quantities",
                "ck_stock_movements_whole_quantity_delta"
            ],
            checks.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task BaselineSeedsCountriesAndCatalogPlaceholdersAndKeepsColumnDefaults()
    {
        await using var db = await MigratedDbAsync();

        Assert.Equal(52, await db.Countries.CountAsync());
        Assert.Equal(2, await db.MacroCategories.CountAsync());
        Assert.Equal(5, await db.Categories.CountAsync());
        Assert.Equal(3, await db.Collections.CountAsync());
        Assert.Equal(3, await db.Materials.CountAsync());
        Assert.Equal(4, await db.MaterialCompositions.CountAsync());
        Assert.All(await db.Categories.ToListAsync(), category => Assert.True(category.IsVisible));

        var defaults = await QueryAsync(db, """
            SELECT table_name || '.' || column_name, column_default FROM information_schema.columns
            WHERE table_schema = 'public' AND column_default IS NOT NULL
              AND (table_name, column_name) IN (('sizes', 'is_visible'), ('contacts', 'is_deleted'),
                ('products', 'base_purchase_cost'), ('order_items', 'discount_percent'))
            """);
        Assert.Equal("true", defaults["sizes.is_visible"]);
        Assert.Equal("false", defaults["contacts.is_deleted"]);
        Assert.Equal("0.0", defaults["products.base_purchase_cost"]);
        Assert.Equal("0.0", defaults["order_items.discount_percent"]);
    }

    [Fact]
    public async Task BaselineSchemaHasNoLegacyMarkupColumnsAndProductAndVariantRoundTrip()
    {
        await using var db = await MigratedDbAsync();
        var markupColumns = await QueryAsync(db, """
            SELECT table_name || '.' || column_name, data_type FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name IN ('products', 'product_variants')
              AND column_name IN ('default_markup_rate', 'markup_rate')
            """);
        Assert.Empty(markupColumns);

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
    public async Task BaselineDownRemovesEveryTable()
    {
        await using var db = await MigratedDbAsync();

        await db.GetService<IMigrator>().MigrateAsync("0");

        var tables = await QueryAsync(db,
            "SELECT tablename, tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'");
        Assert.Empty(tables);
    }

    // The fixture creates databases with EnsureCreated: reset the schema so the baseline migration builds it instead.
    private async Task<Mauw.Erp.Infrastructure.DbContext.MauwDbContext> MigratedDbAsync()
    {
        var db = postgres.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
        await db.GetService<IMigrator>().MigrateAsync();
        return db;
    }

    private static async Task<Dictionary<string, string>> QueryAsync(DbContext db, string sql)
    {
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = new Dictionary<string, string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }
}
