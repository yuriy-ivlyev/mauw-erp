using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInvoiceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bank_name",
                table: "contacts",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bic_swift",
                table: "contacts",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "iban",
                table: "contacts",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_notes",
                table: "contacts",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_share_capital_fully_paid",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bank_name",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "bic_swift",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "iban",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "invoice_notes",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "is_share_capital_fully_paid",
                table: "contacts");
        }
    }
}
