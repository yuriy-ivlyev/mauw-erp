using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PayableDocumentInvoiceNumberScopedByDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payable_documents_supplier_id_source_document_number",
                table: "payable_documents");

            migrationBuilder.CreateIndex(
                name: "IX_payable_documents_supplier_id_source_document_number_docume~",
                table: "payable_documents",
                columns: new[] { "supplier_id", "source_document_number", "document_date" },
                unique: true,
                filter: "supplier_id IS NOT NULL AND source_document_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payable_documents_supplier_id_source_document_number_docume~",
                table: "payable_documents");

            migrationBuilder.CreateIndex(
                name: "IX_payable_documents_supplier_id_source_document_number",
                table: "payable_documents",
                columns: new[] { "supplier_id", "source_document_number" },
                unique: true,
                filter: "supplier_id IS NOT NULL AND source_document_number IS NOT NULL");
        }
    }
}
