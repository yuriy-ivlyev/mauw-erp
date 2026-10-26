using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceWholeMerchandiseQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_whole_quantity_delta",
                table: "stock_movements",
                sql: "quantity_delta IS NULL OR quantity_delta = trunc(quantity_delta)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_items_whole_quantities",
                table: "stock_items",
                sql: "quantity_on_hand = trunc(quantity_on_hand) AND quantity_reserved = trunc(quantity_reserved)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_list_rules_whole_minimum_quantity",
                table: "price_list_rules",
                sql: "minimum_quantity = trunc(minimum_quantity)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_whole_quantity_delta",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_items_whole_quantities",
                table: "stock_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_list_rules_whole_minimum_quantity",
                table: "price_list_rules");
        }
    }
}
