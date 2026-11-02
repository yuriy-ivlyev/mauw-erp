using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditNoteIssuance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "related_invoice_date",
                table: "credit_notes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "related_invoice_number",
                table: "credit_notes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "return_to_warehouse",
                table: "credit_notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sdi_status",
                table: "credit_notes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "warehouse_id",
                table: "credit_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "credit_note_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_invoice_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_shipping_charge = table.Column<bool>(type: "boolean", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    unit_net_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    line_net_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    line_vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    line_gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    nature_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    discount_percent = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_credit_note_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_credit_note_items__invoice_items_source_invoice_item_id",
                        column: x => x.source_invoice_item_id,
                        principalTable: "invoice_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_credit_note_items__product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_credit_note_items_credit_notes_credit_note_id",
                        column: x => x.credit_note_id,
                        principalTable: "credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_credit_notes_warehouse_id",
                table: "credit_notes",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "i_x_credit_note_items_source_invoice_item_id",
                table: "credit_note_items",
                column: "source_invoice_item_id");

            migrationBuilder.CreateIndex(
                name: "i_x_credit_note_items_variant_id",
                table: "credit_note_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_note_items_credit_note_id_source_invoice_item_id",
                table: "credit_note_items",
                columns: new[] { "credit_note_id", "source_invoice_item_id" });

            migrationBuilder.AddForeignKey(
                name: "f_k_credit_notes__warehouses_warehouse_id",
                table: "credit_notes",
                column: "warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_credit_notes__warehouses_warehouse_id",
                table: "credit_notes");

            migrationBuilder.DropTable(
                name: "credit_note_items");

            migrationBuilder.DropIndex(
                name: "i_x_credit_notes_warehouse_id",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "related_invoice_date",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "related_invoice_number",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "return_to_warehouse",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "sdi_status",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "credit_notes");
        }
    }
}
