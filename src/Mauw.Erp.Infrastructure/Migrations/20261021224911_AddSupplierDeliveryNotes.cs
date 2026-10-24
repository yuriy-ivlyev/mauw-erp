using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDeliveryNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "stock_movements",
                type: "numeric(23,8)",
                precision: 23,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<Guid>(
                name: "reference_id",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_delivery_note_line_id",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "stock_items",
                type: "numeric(23,8)",
                precision: 23,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "supplier_article_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_item_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    article_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_article_mappings", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_article_mappings_contacts_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_article_mappings_product_variants_article_variant_~",
                        column: x => x.article_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_delivery_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    document_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_delivery_notes", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_notes__warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_notes_contacts_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_delivery_note_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_delivery_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    supplier_item_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    article_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    line_status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_delivery_note_lines", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_note_lines_product_variants_article_varia~",
                        column: x => x.article_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_note_lines_supplier_delivery_notes_suppli~",
                        column: x => x.supplier_delivery_note_id,
                        principalTable: "supplier_delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_stock_movements_supplier_delivery_note_line_id",
                table: "stock_movements",
                column: "supplier_delivery_note_line_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_article_mappings_article_variant_id",
                table: "supplier_article_mappings",
                column: "article_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_article_mappings_supplier_id_supplier_item_code",
                table: "supplier_article_mappings",
                columns: new[] { "supplier_id", "supplier_item_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_note_lines_article_variant_id",
                table: "supplier_delivery_note_lines",
                column: "article_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_delivery_note_lines_supplier_delivery_note_id_posi~",
                table: "supplier_delivery_note_lines",
                columns: new[] { "supplier_delivery_note_id", "position" });

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_notes_supplier_id",
                table: "supplier_delivery_notes",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_notes_warehouse_id",
                table: "supplier_delivery_notes",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_delivery_notes_uploaded_at_id",
                table: "supplier_delivery_notes",
                columns: new[] { "uploaded_at", "id" },
                descending: new bool[0]);

            migrationBuilder.AddForeignKey(
                name: "f_k_stock_movements__supplier_delivery_note_lines_supplier_delivery~",
                table: "stock_movements",
                column: "supplier_delivery_note_line_id",
                principalTable: "supplier_delivery_note_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_stock_movements__supplier_delivery_note_lines_supplier_delivery~",
                table: "stock_movements");

            migrationBuilder.DropTable(
                name: "supplier_article_mappings");

            migrationBuilder.DropTable(
                name: "supplier_delivery_note_lines");

            migrationBuilder.DropTable(
                name: "supplier_delivery_notes");

            migrationBuilder.DropIndex(
                name: "i_x_stock_movements_supplier_delivery_note_line_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "reference_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "supplier_delivery_note_line_id",
                table: "stock_movements");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "stock_movements",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(23,8)",
                oldPrecision: 23,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "stock_items",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(23,8)",
                oldPrecision: 23,
                oldScale: 8,
                oldNullable: true);
        }
    }
}
