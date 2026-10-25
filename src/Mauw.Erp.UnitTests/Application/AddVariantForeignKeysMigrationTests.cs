using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class AddVariantForeignKeysMigrationTests(PostgreSqlContainerFixture postgres)
{
    private const string BaselineMigration = "20261025191133_InitialCreate";

    private static readonly (string Table, string Constraint, char DeleteAction)[] VariantForeignKeys =
    [
        ("ddt_items", "f_k_ddt_items__product_variants_variant_id", 'r'),
        ("invoice_items", "f_k_invoice_items__product_variants_variant_id", 'r'),
        ("order_items", "f_k_order_items__product_variants_variant_id", 'r'),
        ("stock_movements", "f_k_stock_movements_product_variants_variant_id", 'c')
    ];

    [Fact]
    public async Task MigrationCreatesTheFourVariantForeignKeysWithTheDeclaredDeleteBehavior()
    {
        await using var db = await MigratedDbAsync();

        var foreignKeys = await QueryAsync(db, """
            SELECT conname, conrelid::regclass::text || '|' || confrelid::regclass::text || '|' || confdeltype::text
                || '|' || (SELECT string_agg(a.attname, ',') FROM pg_attribute a
                           WHERE a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey))
                || '|' || (SELECT string_agg(a.attname, ',') FROM pg_attribute a
                           WHERE a.attrelid = c.confrelid AND a.attnum = ANY (c.confkey))
            FROM pg_constraint c WHERE contype = 'f' AND connamespace = 'public'::regnamespace
            """);

        foreach (var (table, constraint, deleteAction) in VariantForeignKeys)
            Assert.Equal($"{table}|product_variants|{deleteAction}|variant_id|id", foreignKeys[constraint]);
    }

    [Fact]
    public async Task MigrationRejectsRowsReferencingMissingVariantsInEveryTable()
    {
        await using var db = await MigratedDbAsync();
        var (orderId, invoiceId, ddtId) = await SeedParentsAsync(db);
        var missingVariantId = Guid.NewGuid();

        var inserts = new (string Constraint, string Sql)[]
        {
            (VariantForeignKeys[0].Constraint, ItemInsert("ddt_items", "ddt_document_id", ddtId, missingVariantId)),
            (VariantForeignKeys[1].Constraint, ItemInsert("invoice_items", "invoice_id", invoiceId, missingVariantId)),
            (VariantForeignKeys[2].Constraint, ItemInsert("order_items", "order_id", orderId, missingVariantId)),
            (VariantForeignKeys[3].Constraint, $"""
                INSERT INTO stock_movements (id, variant_id, type, quantity, unit_cost)
                VALUES ('{Guid.NewGuid()}', '{missingVariantId}', 1, 1, 0.0)
                """)
        };

        foreach (var (constraint, sql) in inserts)
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(db, sql));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
            Assert.Equal(constraint, exception.ConstraintName);
        }
    }

    [Fact]
    public async Task MigrationAppliesRestrictToDocumentLinesAndCascadeToStockMovements()
    {
        await using var db = await MigratedDbAsync();
        var (orderId, invoiceId, ddtId) = await SeedParentsAsync(db);

        foreach (var (table, parentColumn, parentId) in new[]
                 {
                     ("ddt_items", "ddt_document_id", ddtId),
                     ("invoice_items", "invoice_id", invoiceId),
                     ("order_items", "order_id", orderId)
                 })
        {
            var variantId = await SeedVariantAsync(db);
            await ExecuteAsync(db, ItemInsert(table, parentColumn, parentId, variantId));

            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(db, $"DELETE FROM product_variants WHERE id = '{variantId}'"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        }

        var movementVariantId = await SeedVariantAsync(db);
        await ExecuteAsync(db, $"""
            INSERT INTO stock_movements (id, variant_id, type, quantity, unit_cost)
            VALUES ('{Guid.NewGuid()}', '{movementVariantId}', 1, 1, 0.0)
            """);
        await ExecuteAsync(db, $"DELETE FROM product_variants WHERE id = '{movementVariantId}'");
        var remaining = await QueryAsync(db,
            $"SELECT 'count', count(*)::text FROM stock_movements WHERE variant_id = '{movementVariantId}'");
        Assert.Equal("0", remaining["count"]);
    }

    [Fact]
    public async Task MigrationMatchesTheModelAndDownRemovesOnlyTheForeignKeys()
    {
        await using var db = await MigratedDbAsync();
        Assert.False(db.Database.HasPendingModelChanges());

        await db.GetService<IMigrator>().MigrateAsync(BaselineMigration);

        var constraints = await QueryAsync(db,
            "SELECT conname, conname FROM pg_constraint WHERE contype = 'f' AND connamespace = 'public'::regnamespace");
        foreach (var (_, constraint, _) in VariantForeignKeys)
            Assert.DoesNotContain(constraint, constraints.Keys);
        Assert.Contains("f_k_stock_items_product_variants_product_variant_id", constraints.Keys);

        var indexes = await QueryAsync(db, "SELECT indexname, indexname FROM pg_indexes WHERE schemaname = 'public'");
        Assert.Contains("i_x_ddt_items_variant_id", indexes.Keys);
        Assert.Contains("i_x_invoice_items_variant_id", indexes.Keys);
        Assert.Contains("i_x_order_items_variant_id", indexes.Keys);
        Assert.Contains("IX_stock_movements_variant_id_created_at_id", indexes.Keys);

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Equal([BaselineMigration], applied);

        await db.GetService<IMigrator>().MigrateAsync();
        var restored = await QueryAsync(db,
            "SELECT conname, conname FROM pg_constraint WHERE contype = 'f' AND connamespace = 'public'::regnamespace");
        foreach (var (_, constraint, _) in VariantForeignKeys)
            Assert.Contains(constraint, restored.Keys);
    }

    private static string ItemInsert(string table, string parentColumn, Guid parentId, Guid variantId) => $"""
        INSERT INTO {table} (id, {parentColumn}, variant_id, quantity, unit_net_price, line_net_total, vat_rate)
        VALUES ('{Guid.NewGuid()}', '{parentId}', '{variantId}', 1, 10.00000000, 10.00, 22.00)
        """;

    private static async Task<(Guid OrderId, Guid InvoiceId, Guid DdtId)> SeedParentsAsync(MauwDbContext db)
    {
        var code = Guid.NewGuid().ToString("N")[..12];
        var customer = new Contact($"Cliente FK {code}");
        db.Add(customer);
        await db.SaveChangesAsync();

        var order = new Order(customer.Id, $"ORD-{code}");
        var invoice = new Invoice(customer.Id, $"FATT-{code}");
        var ddt = new DdtDocument(customer.Id, $"DDT-{code}");
        db.AddRange(order, invoice, ddt);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (order.Id, invoice.Id, ddt.Id);
    }

    private static async Task<Guid> SeedVariantAsync(MauwDbContext db)
    {
        var code = $"FK-{Guid.NewGuid():N}";
        var product = new Product(Guid.NewGuid(), code, code, "Articolo FK", 22m, 10.50m);
        var variant = new ProductVariant(product.Id, code, 10.50m, product.SupplierId) { Product = product };
        db.AddRange(product, variant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return variant.Id;
    }

    // The fixture creates databases with EnsureCreated: reset the schema so the migrations build it instead.
    private async Task<MauwDbContext> MigratedDbAsync()
    {
        var db = postgres.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
        await db.GetService<IMigrator>().MigrateAsync();
        return db;
    }

    private static async Task ExecuteAsync(DbContext db, string sql)
    {
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
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
