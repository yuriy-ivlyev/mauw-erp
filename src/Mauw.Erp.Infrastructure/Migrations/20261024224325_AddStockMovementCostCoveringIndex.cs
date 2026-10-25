using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementCostCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS i_x_stock_movements_cost_warehouse_variant_date " +
                "ON stock_movements (warehouse_id, variant_id, movement_date_utc DESC, id DESC) " +
                "INCLUDE (quantity_delta, quantity, type, unit_cost) " +
                "WHERE warehouse_id IS NOT NULL AND quantity_delta IS NOT NULL AND quantity_delta > 0 AND unit_cost > 0;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS i_x_stock_movements_cost_warehouse_variant_date;",
                suppressTransaction: true);
        }
    }
}
