using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDocumentOcrPiecesAlias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "supplier_document_ocr_header_aliases",
                columns: new[] { "id", "alias", "column", "created_at", "normalized_alias", "position", "updated_at" },
                values: new object[] { new Guid("d5510000-0000-0000-0000-000000000049"), "pz", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "pz", 13, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "supplier_document_ocr_header_aliases",
                keyColumn: "id",
                keyValue: new Guid("d5510000-0000-0000-0000-000000000049"));
        }
    }
}
