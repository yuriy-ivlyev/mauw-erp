using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyMarkupRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_markup_rate",
                table: "products");

            migrationBuilder.DropColumn(
                name: "markup_rate",
                table: "product_variants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "default_markup_rate",
                table: "products",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "markup_rate",
                table: "product_variants",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);
        }
    }
}
