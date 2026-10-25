using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDdtWarehouseAndStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "warehouse_id",
                table: "ddt_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_ddt_documents_warehouse_id",
                table: "ddt_documents",
                column: "warehouse_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_ddt_documents__warehouses_warehouse_id",
                table: "ddt_documents",
                column: "warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_ddt_documents__warehouses_warehouse_id",
                table: "ddt_documents");

            migrationBuilder.DropIndex(
                name: "i_x_ddt_documents_warehouse_id",
                table: "ddt_documents");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "ddt_documents");
        }
    }
}
