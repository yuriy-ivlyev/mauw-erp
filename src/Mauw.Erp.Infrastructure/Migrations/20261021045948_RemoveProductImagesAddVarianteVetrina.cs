using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductImagesAddVarianteVetrina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.AddColumn<bool>(
                name: "variante_vetrina",
                table: "product_variants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "variante_vetrina",
                table: "product_variants");

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_product_images", x => x.id);
                    table.ForeignKey(
                        name: "f_k_product_images_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_product_images_product_id",
                table: "product_images",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_images_product_id_display_order",
                table: "product_images",
                columns: new[] { "product_id", "display_order" });
        }
    }
}
