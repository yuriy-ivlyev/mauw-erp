using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameProductInternalCodeToBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "internal_code",
                table: "products",
                newName: "barcode");

            migrationBuilder.RenameIndex(
                name: "IX_products_internal_code",
                table: "products",
                newName: "IX_products_barcode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "barcode",
                table: "products",
                newName: "internal_code");

            migrationBuilder.RenameIndex(
                name: "IX_products_barcode",
                table: "products",
                newName: "IX_products_internal_code");
        }
    }
}
