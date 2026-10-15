using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedColorAndSizeData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed Sizes
            migrationBuilder.InsertData(
                table: "sizes",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { "NA___", "N/A" },
                    { "XXS__", "XXS" },
                    { "XS___", "XS" },
                    { "SM___", "SM" },
                    { "MD___", "MD" },
                    { "LG___", "LG" },
                    { "XL___", "XL" },
                    { "XXL__", "XXL" },
                    { "XXXL_", "XXXL" }
                });

            // Seed Colors
            migrationBuilder.InsertData(
                table: "colors",
                columns: new[] { "id", "name", "html_color" },
                values: new object[,]
                {
                    { "BLK", "Nero", "#1A1A1A" },
                    { "WHT", "Bianco", "#FFFFFF" },
                    { "GRY", "Grigio", "#808080" },
                    { "YLW", "Giallo", "#FFFF00" },
                    { "VLT", "Viola", "#800080" },
                    { "RED", "Rosso", "#FF0000" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete Sizes
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "NA___" });
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "XXS__" });
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "XS___" });
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "SM___" });
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "MD___" });
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "LG___" });
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "XL___" });
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "XXL__" });
            migrationBuilder.DeleteData(
                table: "sizes",
                keyColumn: "id",
                keyValues: new object[] { "XXXL_" });

            // Delete Colors
            migrationBuilder.DeleteData(
                table: "colors",
                keyColumn: "id",
                keyValues: new object[] { "BLK" });
            migrationBuilder.DeleteData(
                table: "colors",
                keyColumn: "id",
                keyValues: new object[] { "WHT" });
            migrationBuilder.DeleteData(
                table: "colors",
                keyColumn: "id",
                keyValues: new object[] { "GRY" });
            migrationBuilder.DeleteData(
                table: "colors",
                keyColumn: "id",
                keyValues: new object[] { "YLW" });
            migrationBuilder.DeleteData(
                table: "colors",
                keyColumn: "id",
                keyValues: new object[] { "VLT" });
            migrationBuilder.DeleteData(
                table: "colors",
                keyColumn: "id",
                keyValues: new object[] { "RED" });
        }
    }
}
