using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorProductVariantsAndAddSupportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "size",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "stock_quantity",
                table: "product_variants");

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "collection_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "material_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "color_id",
                table: "product_variants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "size_id",
                table: "product_variants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_collections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "colors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    html_color = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_colors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "macro_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_macro_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "materials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_materials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sizes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_sizes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    macro_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_categories", x => x.id);
                    table.ForeignKey(
                        name: "f_k_categories__macro_categories_macro_category_id",
                        column: x => x.macro_category_id,
                        principalTable: "macro_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "material_compositions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    percentage_value = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_material_compositions", x => x.id);
                    table.ForeignKey(
                        name: "f_k_material_compositions_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "i_x_products_collection_id",
                table: "products",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "i_x_products_material_id",
                table: "products",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "i_x_product_variants_color_id",
                table: "product_variants",
                column: "color_id");

            migrationBuilder.CreateIndex(
                name: "i_x_product_variants_size_id",
                table: "product_variants",
                column: "size_id");

            migrationBuilder.CreateIndex(
                name: "i_x_categories_macro_category_id",
                table: "categories",
                column: "macro_category_id");

            migrationBuilder.CreateIndex(
                name: "i_x_material_compositions_material_id",
                table: "material_compositions",
                column: "material_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_product_variants__sizes_size_id",
                table: "product_variants",
                column: "size_id",
                principalTable: "sizes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_product_variants_colors_color_id",
                table: "product_variants",
                column: "color_id",
                principalTable: "colors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_products_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "f_k_products_collections_collection_id",
                table: "products",
                column: "collection_id",
                principalTable: "collections",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "f_k_products_materials_material_id",
                table: "products",
                column: "material_id",
                principalTable: "materials",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Seed default sizes
            var sizesData = new object[,]
            {
                { Guid.NewGuid(), "T/U", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "XXS", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "XS", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "S", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "M", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "L", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "XL", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "XXL", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "X3L", DateTime.UtcNow, DateTime.UtcNow }
            };

            migrationBuilder.InsertData(
                table: "sizes",
                columns: new[] { "id", "name", "created_at", "updated_at" },
                values: sizesData);

            // Seed default N/A color
            migrationBuilder.InsertData(
                table: "colors",
                columns: new[] { "id", "name", "html_color", "created_at", "updated_at" },
                values: new object[] { Guid.NewGuid(), "N/A", "#000000", DateTime.UtcNow, DateTime.UtcNow });

            // Seed macro categories
            var clothingMacroId = Guid.NewGuid();
            var accessoriesMacroId = Guid.NewGuid();
            
            var macroCategoriesData = new object[,]
            {
                { clothingMacroId, "Clothing", DateTime.UtcNow, DateTime.UtcNow },
                { accessoriesMacroId, "Accessories", DateTime.UtcNow, DateTime.UtcNow }
            };

            migrationBuilder.InsertData(
                table: "macro_categories",
                columns: new[] { "id", "name", "created_at", "updated_at" },
                values: macroCategoriesData);

            // Seed categories
            var categoriesData = new object[,]
            {
                { Guid.NewGuid(), "T-Shirts", clothingMacroId, DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "Pants", clothingMacroId, DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "Shoes", clothingMacroId, DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "Bags", accessoriesMacroId, DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "Hats", accessoriesMacroId, DateTime.UtcNow, DateTime.UtcNow }
            };

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "name", "macro_category_id", "created_at", "updated_at" },
                values: categoriesData);

            // Seed collections
            var collectionsData = new object[,]
            {
                { Guid.NewGuid(), "Summer 2024", "Summer collection 2024", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "Winter 2024", "Winter collection 2024", DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), "Spring 2025", "Spring collection 2025", DateTime.UtcNow, DateTime.UtcNow }
            };

            migrationBuilder.InsertData(
                table: "collections",
                columns: new[] { "id", "name", "description", "created_at", "updated_at" },
                values: collectionsData);

            // Seed materials with compositions
            var cottonMaterialId = Guid.NewGuid();
            var polyesterMaterialId = Guid.NewGuid();
            var blendMaterialId = Guid.NewGuid();

            var materialsData = new object[,]
            {
                { cottonMaterialId, "100% Cotton", DateTime.UtcNow, DateTime.UtcNow },
                { polyesterMaterialId, "Polyester", DateTime.UtcNow, DateTime.UtcNow },
                { blendMaterialId, "Cotton Blend", DateTime.UtcNow, DateTime.UtcNow }
            };

            migrationBuilder.InsertData(
                table: "materials",
                columns: new[] { "id", "name", "created_at", "updated_at" },
                values: materialsData);

            // Seed material compositions
            var compositionsData = new object[,]
            {
                { Guid.NewGuid(), cottonMaterialId, "Cotton", (decimal)100.00, DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), polyesterMaterialId, "Polyester", (decimal)100.00, DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), blendMaterialId, "Cotton", (decimal)70.00, DateTime.UtcNow, DateTime.UtcNow },
                { Guid.NewGuid(), blendMaterialId, "Polyester", (decimal)30.00, DateTime.UtcNow, DateTime.UtcNow }
            };

            migrationBuilder.InsertData(
                table: "material_compositions",
                columns: new[] { "id", "material_id", "component_name", "percentage_value", "created_at", "updated_at" },
                values: compositionsData);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_product_variants__sizes_size_id",
                table: "product_variants");

            migrationBuilder.DropForeignKey(
                name: "f_k_product_variants_colors_color_id",
                table: "product_variants");

            migrationBuilder.DropForeignKey(
                name: "f_k_products_categories_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "f_k_products_collections_collection_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "f_k_products_materials_material_id",
                table: "products");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "collections");

            migrationBuilder.DropTable(
                name: "colors");

            migrationBuilder.DropTable(
                name: "material_compositions");

            migrationBuilder.DropTable(
                name: "sizes");

            migrationBuilder.DropTable(
                name: "macro_categories");

            migrationBuilder.DropTable(
                name: "materials");

            migrationBuilder.DropIndex(
                name: "i_x_products_category_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "i_x_products_collection_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "i_x_products_material_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "i_x_product_variants_color_id",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "i_x_product_variants_size_id",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "material_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "color_id",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "size_id",
                table: "product_variants");

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "product_variants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "size",
                table: "product_variants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "stock_quantity",
                table: "product_variants",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
