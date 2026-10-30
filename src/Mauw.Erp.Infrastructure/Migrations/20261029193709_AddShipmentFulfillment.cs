using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "shipment_document_number_seq");

            migrationBuilder.AddColumn<int>(
                name: "quantity_shipped",
                table: "order_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "shipment_id",
                table: "ddt_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ddt_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    carrier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tracking_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    box_count = table.Column<int>(type: "integer", nullable: false),
                    total_weight_kg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    shipped_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_shipments", x => x.id);
                    table.ForeignKey(
                        name: "f_k_shipments_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quantity_shipped = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_shipment_lines", x => x.id);
                    table.ForeignKey(
                        name: "f_k_shipment_lines_order_items_order_line_id",
                        column: x => x.order_line_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_shipment_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_shipment_lines_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_ddt_documents_shipment_id",
                table: "ddt_documents",
                column: "shipment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_shipment_lines_order_line_id",
                table: "shipment_lines",
                column: "order_line_id");

            migrationBuilder.CreateIndex(
                name: "i_x_shipment_lines_product_id",
                table: "shipment_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_lines_shipment_id_order_line_id",
                table: "shipment_lines",
                columns: new[] { "shipment_id", "order_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_shipments_order_id",
                table: "shipments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_ddt_number",
                table: "shipments",
                column: "ddt_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_shipment_number",
                table: "shipments",
                column: "shipment_number",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "f_k_ddt_documents__shipments_shipment_id",
                table: "ddt_documents",
                column: "shipment_id",
                principalTable: "shipments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_ddt_documents__shipments_shipment_id",
                table: "ddt_documents");

            migrationBuilder.DropTable(
                name: "shipment_lines");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropIndex(
                name: "i_x_ddt_documents_shipment_id",
                table: "ddt_documents");

            migrationBuilder.DropColumn(
                name: "quantity_shipped",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "shipment_id",
                table: "ddt_documents");

            migrationBuilder.DropSequence(
                name: "shipment_document_number_seq");
        }
    }
}
