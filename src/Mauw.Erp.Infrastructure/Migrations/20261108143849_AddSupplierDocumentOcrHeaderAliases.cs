using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDocumentOcrHeaderAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier_document_ocr_header_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    column = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_document_ocr_header_aliases", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "supplier_document_ocr_header_aliases",
                columns: new[] { "id", "alias", "column", "created_at", "normalized_alias", "position", "updated_at" },
                values: new object[,]
                {
                    { new Guid("d5510000-0000-0000-0000-000000000001"), "codicearticolo", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "codicearticolo", 0, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000002"), "codart", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "codart", 1, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000003"), "codicefornitore", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "codicefornitore", 2, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000004"), "codiceprodotto", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "codiceprodotto", 3, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000005"), "codice", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "codice", 4, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000006"), "suppliercode", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "suppliercode", 5, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000007"), "supplieritemcode", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "supplieritemcode", 6, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000008"), "itemcode", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "itemcode", 7, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000009"), "itemnumber", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "itemnumber", 8, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000010"), "itemno", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "itemno", 9, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000011"), "productcode", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "productcode", 10, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000012"), "productnumber", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "productnumber", 11, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000013"), "articlecode", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "articlecode", 12, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000014"), "articleno", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "articleno", 13, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000015"), "numeroarticolo", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "numeroarticolo", 14, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000016"), "nrarticolo", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "nrarticolo", 15, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000017"), "nart", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "nart", 16, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000018"), "code", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "code", 17, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000019"), "sku", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "sku", 18, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000020"), "reference", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "reference", 19, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000021"), "ref", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "ref", 20, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000022"), "partnumber", "SupplierCode", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "partnumber", 21, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000023"), "descrizione", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "descrizione", 0, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000024"), "description", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "description", 1, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000025"), "itemdescription", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "itemdescription", 2, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000026"), "productdescription", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "productdescription", 3, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000027"), "descrizionearticolo", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "descrizionearticolo", 4, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000028"), "descrarticolo", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "descrarticolo", 5, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000029"), "descrizionemerce", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "descrizionemerce", 6, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000030"), "denominazione", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "denominazione", 7, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000031"), "nomearticolo", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "nomearticolo", 8, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000032"), "dettaglio", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "dettaglio", 9, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000033"), "articolo", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "articolo", 10, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000034"), "productname", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "productname", 11, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000035"), "itemname", "Description", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "itemname", 12, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000036"), "quantita", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "quantita", 0, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000037"), "qta", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "qta", 1, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000038"), "qtaord", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "qtaord", 2, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000039"), "quantitaordinata", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "quantitaordinata", 3, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000040"), "qty", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "qty", 4, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000041"), "qtyordered", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "qtyordered", 5, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000042"), "quantity", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "quantity", 6, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000043"), "quantities", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "quantities", 7, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000044"), "quantityordered", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "quantityordered", 8, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000045"), "npezzi", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "npezzi", 9, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000046"), "pezzi", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "pezzi", 10, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000047"), "pieces", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "pieces", 11, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5510000-0000-0000-0000-000000000048"), "pcs", "Quantity", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), "pcs", 12, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_document_ocr_header_aliases_column_position",
                table: "supplier_document_ocr_header_aliases",
                columns: new[] { "column", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_document_ocr_header_aliases_normalized_alias",
                table: "supplier_document_ocr_header_aliases",
                column: "normalized_alias",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_document_ocr_header_aliases");
        }
    }
}
