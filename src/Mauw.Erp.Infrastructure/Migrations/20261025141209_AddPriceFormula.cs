using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceFormula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "price_formulas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rounding_step = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    updated_by_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_singleton = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_price_formulas", x => x.id);
                    table.CheckConstraint("ck_price_formulas_is_singleton", "is_singleton");
                });

            migrationBuilder.CreateTable(
                name: "price_formula_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_formula_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_price_formula_steps", x => x.id);
                    table.CheckConstraint("ck_price_formula_steps_percentage", "percentage >= 0 AND percentage <= 9999");
                    table.CheckConstraint("ck_price_formula_steps_position", "\"position\" >= 1");
                    table.ForeignKey(
                        name: "f_k_price_formula_steps_price_formulas_price_formula_id",
                        column: x => x.price_formula_id,
                        principalTable: "price_formulas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_price_formula_steps_price_formula_id_position",
                table: "price_formula_steps",
                columns: new[] { "price_formula_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_price_formulas_is_singleton",
                table: "price_formulas",
                column: "is_singleton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_formula_steps");

            migrationBuilder.DropTable(
                name: "price_formulas");
        }
    }
}
