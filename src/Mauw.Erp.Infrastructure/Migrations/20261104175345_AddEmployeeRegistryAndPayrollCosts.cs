using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeRegistryAndPayrollCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "employee_id",
                table: "payable_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    qualification = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    contract_level = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    contract_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    annual_gross_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    annual_company_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_employees", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payable_documents_employee_id_source_document_number",
                table: "payable_documents",
                columns: new[] { "employee_id", "source_document_number" },
                unique: true,
                filter: "kind = 2 AND employee_id IS NOT NULL AND source_document_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_employees_employee_number",
                table: "employees",
                column: "employee_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_is_active_employee_number",
                table: "employees",
                columns: new[] { "is_active", "employee_number" });

            migrationBuilder.AddForeignKey(
                name: "f_k_payable_documents_employees_employee_id",
                table: "payable_documents",
                column: "employee_id",
                principalTable: "employees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_payable_documents_employees_employee_id",
                table: "payable_documents");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropIndex(
                name: "IX_payable_documents_employee_id_source_document_number",
                table: "payable_documents");

            migrationBuilder.DropColumn(
                name: "employee_id",
                table: "payable_documents");
        }
    }
}
