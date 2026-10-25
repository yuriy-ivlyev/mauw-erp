using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The model already declares these FKs (snapshot unchanged); the baseline schema lacked them.
            // The indexes on variant_id already exist, so only the constraints are added.
            migrationBuilder.AddForeignKey(
                name: "f_k_ddt_items__product_variants_variant_id",
                table: "ddt_items",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_invoice_items__product_variants_variant_id",
                table: "invoice_items",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_order_items__product_variants_variant_id",
                table: "order_items",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_stock_movements_product_variants_variant_id",
                table: "stock_movements",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_ddt_items__product_variants_variant_id",
                table: "ddt_items");

            migrationBuilder.DropForeignKey(
                name: "f_k_invoice_items__product_variants_variant_id",
                table: "invoice_items");

            migrationBuilder.DropForeignKey(
                name: "f_k_order_items__product_variants_variant_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "f_k_stock_movements_product_variants_variant_id",
                table: "stock_movements");
        }
    }
}
