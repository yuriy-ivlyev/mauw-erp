using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceListsAndPricingSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "pricing_agreed_unit_net_price",
                table: "order_items",
                type: "numeric(22,8)",
                precision: 22,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pricing_is_customer_return",
                table: "order_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pricing_is_manual_override",
                table: "order_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pricing_list_unit_price",
                table: "order_items",
                type: "numeric(22,8)",
                precision: 22,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pricing_override_reason",
                table: "order_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_price_list_id",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_rule_id",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_source_line_id",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pricing_agreed_unit_net_price",
                table: "invoice_items",
                type: "numeric(22,8)",
                precision: 22,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pricing_is_customer_return",
                table: "invoice_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pricing_is_manual_override",
                table: "invoice_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pricing_list_unit_price",
                table: "invoice_items",
                type: "numeric(22,8)",
                precision: 22,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pricing_override_reason",
                table: "invoice_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_price_list_id",
                table: "invoice_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_rule_id",
                table: "invoice_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_source_line_id",
                table: "invoice_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pricing_agreed_unit_net_price",
                table: "ddt_items",
                type: "numeric(22,8)",
                precision: 22,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pricing_is_customer_return",
                table: "ddt_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pricing_is_manual_override",
                table: "ddt_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pricing_list_unit_price",
                table: "ddt_items",
                type: "numeric(22,8)",
                precision: 22,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pricing_override_reason",
                table: "ddt_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_price_list_id",
                table: "ddt_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_rule_id",
                table: "ddt_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_source_line_id",
                table: "ddt_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "price_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_price_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "price_list_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculation_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    value = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    minimum_quantity = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_price_list_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_list_rules_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_price_list_rules_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_price_list_rules_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_price_list_rules_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_customer_profiles_price_list_id",
                table: "customer_profiles",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_category_id",
                table: "price_list_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_price_list_id_category_id",
                table: "price_list_rules",
                columns: new[] { "price_list_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_price_list_id_product_id",
                table: "price_list_rules",
                columns: new[] { "price_list_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_price_list_id_variant_id",
                table: "price_list_rules",
                columns: new[] { "price_list_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_product_id",
                table: "price_list_rules",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_variant_id",
                table: "price_list_rules",
                column: "variant_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_customer_profiles__price_lists_price_list_id",
                table: "customer_profiles",
                column: "price_list_id",
                principalTable: "price_lists",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_customer_profiles__price_lists_price_list_id",
                table: "customer_profiles");

            migrationBuilder.DropTable(
                name: "price_list_rules");

            migrationBuilder.DropTable(
                name: "price_lists");

            migrationBuilder.DropIndex(
                name: "i_x_customer_profiles_price_list_id",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "pricing_agreed_unit_net_price",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "pricing_is_customer_return",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "pricing_is_manual_override",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "pricing_list_unit_price",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "pricing_override_reason",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "pricing_price_list_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "pricing_rule_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "pricing_source_line_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "pricing_agreed_unit_net_price",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "pricing_is_customer_return",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "pricing_is_manual_override",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "pricing_list_unit_price",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "pricing_override_reason",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "pricing_price_list_id",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "pricing_rule_id",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "pricing_source_line_id",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "pricing_agreed_unit_net_price",
                table: "ddt_items");

            migrationBuilder.DropColumn(
                name: "pricing_is_customer_return",
                table: "ddt_items");

            migrationBuilder.DropColumn(
                name: "pricing_is_manual_override",
                table: "ddt_items");

            migrationBuilder.DropColumn(
                name: "pricing_list_unit_price",
                table: "ddt_items");

            migrationBuilder.DropColumn(
                name: "pricing_override_reason",
                table: "ddt_items");

            migrationBuilder.DropColumn(
                name: "pricing_price_list_id",
                table: "ddt_items");

            migrationBuilder.DropColumn(
                name: "pricing_rule_id",
                table: "ddt_items");

            migrationBuilder.DropColumn(
                name: "pricing_source_line_id",
                table: "ddt_items");
        }
    }
}
