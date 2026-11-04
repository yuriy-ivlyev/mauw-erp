using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayableDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payable_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    document_code = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    beneficiary_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    approved_by_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    taxable_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    attachment_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    attachment_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    attachment_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attachment_length = table.Column<long>(type: "bigint", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_payable_documents", x => x.id);
                    table.ForeignKey(
                        name: "f_k_payable_documents_contacts_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payable_document_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payable_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    cost_center = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    project = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    taxable_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    nature_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_payable_document_lines", x => x.id);
                    table.ForeignKey(
                        name: "f_k_payable_document_lines_payable_documents_payable_document_id",
                        column: x => x.payable_document_id,
                        principalTable: "payable_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payable_document_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payable_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    transaction_details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_payable_document_payments", x => x.id);
                    table.ForeignKey(
                        name: "f_k_payable_document_payments_payable_documents_payable_documen~",
                        column: x => x.payable_document_id,
                        principalTable: "payable_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_payable_document_lines_payable_document_id",
                table: "payable_document_lines",
                column: "payable_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_payable_document_payments_payable_document_id_payment_date_~",
                table: "payable_document_payments",
                columns: new[] { "payable_document_id", "payment_date_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_payable_documents_created_by_user_id",
                table: "payable_documents",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payable_documents_document_code",
                table: "payable_documents",
                column: "document_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payable_documents_kind_document_date",
                table: "payable_documents",
                columns: new[] { "kind", "document_date" });

            migrationBuilder.CreateIndex(
                name: "IX_payable_documents_status_due_date",
                table: "payable_documents",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "IX_payable_documents_supplier_id_source_document_number",
                table: "payable_documents",
                columns: new[] { "supplier_id", "source_document_number" },
                unique: true,
                filter: "supplier_id IS NOT NULL AND source_document_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payable_document_lines");

            migrationBuilder.DropTable(
                name: "payable_document_payments");

            migrationBuilder.DropTable(
                name: "payable_documents");
        }
    }
}
