using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistDefinitiveSellingPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "fixed_selling_price",
                table: "products",
                newName: "selling_price");

            migrationBuilder.RenameColumn(
                name: "fixed_selling_price",
                table: "product_variants",
                newName: "selling_price");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "selling_price",
                table: "products",
                newName: "fixed_selling_price");

            migrationBuilder.RenameColumn(
                name: "selling_price",
                table: "product_variants",
                newName: "fixed_selling_price");
        }
    }
}
