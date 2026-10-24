using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "supplier_purchase_order_number_seq");

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_purchase_order_id",
                table: "supplier_delivery_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_purchase_order_line_id",
                table: "supplier_delivery_note_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "supplier_purchase_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_purchase_orders", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_purchase_orders_contacts_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_purchase_order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_ordered = table.Column<int>(type: "integer", nullable: false),
                    quantity_received = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_purchase_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_purchase_order_lines_product_variants_article_vari~",
                        column: x => x.article_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_purchase_order_lines_supplier_purchase_orders_supp~",
                        column: x => x.supplier_purchase_order_id,
                        principalTable: "supplier_purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_return_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_delivery_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_return_drafts", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_return_drafts_supplier_delivery_notes_supplier_del~",
                        column: x => x.supplier_delivery_note_id,
                        principalTable: "supplier_delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_return_drafts_supplier_purchase_orders_supplier_pu~",
                        column: x => x.supplier_purchase_order_id,
                        principalTable: "supplier_purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_return_draft_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_return_draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_delivery_note_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_return_draft_lines", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_return_draft_lines_product_variants_article_varian~",
                        column: x => x.article_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_return_draft_lines_supplier_delivery_note_lines_su~",
                        column: x => x.supplier_delivery_note_line_id,
                        principalTable: "supplier_delivery_note_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_return_draft_lines_supplier_return_drafts_supplier~",
                        column: x => x.supplier_return_draft_id,
                        principalTable: "supplier_return_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_notes_supplier_purchase_order_id",
                table: "supplier_delivery_notes",
                column: "supplier_purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_note_lines_supplier_purchase_order_line_id",
                table: "supplier_delivery_note_lines",
                column: "supplier_purchase_order_line_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_purchase_order_lines_article_variant_id",
                table: "supplier_purchase_order_lines",
                column: "article_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_purchase_order_lines_supplier_purchase_order_id_ar~",
                table: "supplier_purchase_order_lines",
                columns: new[] { "supplier_purchase_order_id", "article_variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_purchase_orders_supplier_id",
                table: "supplier_purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_purchase_orders_order_number",
                table: "supplier_purchase_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_purchase_orders_status_supplier_id_order_date",
                table: "supplier_purchase_orders",
                columns: new[] { "status", "supplier_id", "order_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_return_draft_lines_article_variant_id",
                table: "supplier_return_draft_lines",
                column: "article_variant_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_return_draft_lines_supplier_delivery_note_line_id",
                table: "supplier_return_draft_lines",
                column: "supplier_delivery_note_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_draft_lines_supplier_return_draft_id_suppli~",
                table: "supplier_return_draft_lines",
                columns: new[] { "supplier_return_draft_id", "supplier_delivery_note_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_return_drafts_supplier_delivery_note_id",
                table: "supplier_return_drafts",
                column: "supplier_delivery_note_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_return_drafts_supplier_purchase_order_id",
                table: "supplier_return_drafts",
                column: "supplier_purchase_order_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_supplier_delivery_note_lines__supplier_purchase_order_lines_sup~",
                table: "supplier_delivery_note_lines",
                column: "supplier_purchase_order_line_id",
                principalTable: "supplier_purchase_order_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "f_k_supplier_delivery_notes__supplier_purchase_orders_supplier_pur~",
                table: "supplier_delivery_notes",
                column: "supplier_purchase_order_id",
                principalTable: "supplier_purchase_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_supplier_delivery_note_lines__supplier_purchase_order_lines_sup~",
                table: "supplier_delivery_note_lines");

            migrationBuilder.DropForeignKey(
                name: "f_k_supplier_delivery_notes__supplier_purchase_orders_supplier_pur~",
                table: "supplier_delivery_notes");

            migrationBuilder.DropTable(
                name: "supplier_purchase_order_lines");

            migrationBuilder.DropTable(
                name: "supplier_return_draft_lines");

            migrationBuilder.DropTable(
                name: "supplier_return_drafts");

            migrationBuilder.DropTable(
                name: "supplier_purchase_orders");

            migrationBuilder.DropIndex(
                name: "i_x_supplier_delivery_notes_supplier_purchase_order_id",
                table: "supplier_delivery_notes");

            migrationBuilder.DropIndex(
                name: "i_x_supplier_delivery_note_lines_supplier_purchase_order_line_id",
                table: "supplier_delivery_note_lines");

            migrationBuilder.DropColumn(
                name: "supplier_purchase_order_id",
                table: "supplier_delivery_notes");

            migrationBuilder.DropColumn(
                name: "supplier_purchase_order_line_id",
                table: "supplier_delivery_note_lines");

            migrationBuilder.DropSequence(
                name: "supplier_purchase_order_number_seq");
        }
    }
}
