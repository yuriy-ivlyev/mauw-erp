using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIdToInvoiceDdtAndCreditNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "order_id",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "order_id",
                table: "ddt_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_note_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    credit_note_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    taxable_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_credit_notes", x => x.id);
                    table.ForeignKey(
                        name: "f_k_credit_notes__invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_order_id",
                table: "invoices",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_ddt_documents_order_id",
                table: "ddt_documents",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "i_x_credit_notes_invoice_id",
                table: "credit_notes",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_credit_note_number",
                table: "credit_notes",
                column: "credit_note_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropIndex(
                name: "IX_invoices_order_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_ddt_documents_order_id",
                table: "ddt_documents");

            migrationBuilder.DropColumn(
                name: "order_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "order_id",
                table: "ddt_documents");
        }
    }
}
