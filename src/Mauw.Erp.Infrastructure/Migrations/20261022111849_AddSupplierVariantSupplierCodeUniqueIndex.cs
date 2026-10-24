using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierVariantSupplierCodeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "product_variants",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE product_variants AS variant
                SET supplier_id = product.supplier_id
                FROM products AS product
                WHERE product.id = variant.product_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "supplier_id",
                table: "product_variants",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_supplier_id_supplier_code",
                table: "product_variants",
                columns: new[] { "supplier_id", "supplier_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_variants_supplier_id_supplier_code",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "product_variants");
        }
    }
}
