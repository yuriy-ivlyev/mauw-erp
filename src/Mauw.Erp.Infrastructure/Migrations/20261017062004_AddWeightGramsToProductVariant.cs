using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightGramsToProductVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "weight_grams",
                table: "product_variants",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "weight_grams",
                table: "product_variants");
        }
    }
}
