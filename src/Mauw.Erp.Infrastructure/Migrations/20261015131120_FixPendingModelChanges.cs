using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_product_variants__sizes_size_id",
                table: "product_variants");

            migrationBuilder.DropForeignKey(
                name: "f_k_product_variants_colors_color_id",
                table: "product_variants");

            migrationBuilder.AlterColumn<Guid>(
                name: "size_id",
                table: "product_variants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "color_id",
                table: "product_variants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "f_k_product_variants__sizes_size_id",
                table: "product_variants",
                column: "size_id",
                principalTable: "sizes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "f_k_product_variants_colors_color_id",
                table: "product_variants",
                column: "color_id",
                principalTable: "colors",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.AlterColumn<Guid>(
                name: "size_id",
                table: "product_variants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "color_id",
                table: "product_variants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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
        }
    }
}
