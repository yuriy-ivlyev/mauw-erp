using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceOrderSynchronization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_billing_only",
                table: "order_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_excluded_from_invoice",
                table: "order_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "sdi_submitted_at",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "shipping_cost",
                table: "invoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "shipping_nature_code",
                table: "invoices",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "shipping_vat_rate",
                table: "invoices",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_billing_only",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "is_excluded_from_invoice",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "sdi_submitted_at",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "shipping_cost",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "shipping_nature_code",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "shipping_vat_rate",
                table: "invoices");
        }
    }
}
