using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogFixedSellingPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "default_markup_rate",
                table: "products",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "fixed_selling_price",
                table: "products",
                type: "numeric(22,8)",
                precision: 22,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fixed_selling_price",
                table: "product_variants",
                type: "numeric(22,8)",
                precision: 22,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "markup_rate",
                table: "product_variants",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fixed_selling_price",
                table: "products");

            migrationBuilder.DropColumn(
                name: "fixed_selling_price",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "markup_rate",
                table: "product_variants");

            migrationBuilder.AlterColumn<decimal>(
                name: "default_markup_rate",
                table: "products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2);
        }
    }
}
