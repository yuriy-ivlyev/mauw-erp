using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryStockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.Sql(
                "ALTER TABLE stock_movements ADD COLUMN IF NOT EXISTS quantity_delta numeric(18,3);");

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS i_x_stock_movements_reference_id ON stock_movements (reference_id);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS i_x_stock_movements_warehouse_id_movement_date_utc ON stock_movements (warehouse_id, movement_date_utc);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS i_x_stock_movements_warehouse_variant_movement_date_utc ON stock_movements (warehouse_id, variant_id, movement_date_utc DESC);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS i_x_products_name_trgm ON products USING gin (name gin_trgm_ops);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS i_x_products_supplier_code_trgm ON products USING gin (supplier_code gin_trgm_ops);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS i_x_product_variants_supplier_code_trgm ON product_variants USING gin (supplier_code gin_trgm_ops);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS i_x_product_variants_barcode_trgm ON product_variants USING gin (barcode gin_trgm_ops);",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS i_x_stock_movements_reference_id;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS i_x_stock_movements_warehouse_id_movement_date_utc;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS i_x_stock_movements_warehouse_variant_movement_date_utc;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS i_x_products_name_trgm;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS i_x_products_supplier_code_trgm;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS i_x_product_variants_supplier_code_trgm;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS i_x_product_variants_barcode_trgm;",
                suppressTransaction: true);

            migrationBuilder.Sql("ALTER TABLE stock_movements DROP COLUMN IF EXISTS quantity_delta;");

            migrationBuilder.Sql("DROP EXTENSION IF EXISTS pg_trgm RESTRICT;");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
