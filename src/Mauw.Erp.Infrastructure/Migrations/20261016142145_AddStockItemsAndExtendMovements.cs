using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockItemsAndExtendMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "reference_doc",
                table: "stock_movements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "created_by_user_id",
                table: "stock_movements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "movement_date_utc",
                table: "stock_movements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "stock_movements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reason",
                table: "stock_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "warehouse_id",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_on_hand = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    quantity_reserved = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_stock_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_stock_items__warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_stock_items_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_movement_date_utc",
                table: "stock_movements",
                column: "movement_date_utc");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_warehouse_id_variant_id",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "i_x_stock_items_product_variant_id",
                table: "stock_items",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "i_x_stock_items_warehouse_id",
                table: "stock_items",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_warehouse_id_product_variant_id",
                table: "stock_items",
                columns: new[] { "warehouse_id", "product_variant_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "f_k_stock_movements__warehouses_warehouse_id",
                table: "stock_movements",
                column: "warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_stock_movements__warehouses_warehouse_id",
                table: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_movement_date_utc",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_warehouse_id_variant_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "movement_date_utc",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "reason",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "stock_movements");

            migrationBuilder.AlterColumn<string>(
                name: "reference_doc",
                table: "stock_movements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
