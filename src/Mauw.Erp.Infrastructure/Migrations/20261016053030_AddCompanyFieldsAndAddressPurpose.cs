using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyFieldsAndAddressPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_contact_addresses_contact_id",
                table: "contact_addresses");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "contact_addresses",
                newName: "purpose");

            migrationBuilder.RenameColumn(
                name: "is_default_shipping",
                table: "contact_addresses",
                newName: "is_main_operational_site");

            migrationBuilder.AddColumn<bool>(
                name: "is_in_liquidation",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_internal_company",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_sole_shareholder",
                table: "contacts",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                table: "contacts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rea_number",
                table: "contacts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rea_office",
                table: "contacts",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "share_capital",
                table: "contacts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "access_notes",
                table: "contact_addresses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "contact_addresses",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "linked_warehouse_id",
                table: "contact_addresses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "contact_addresses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "site_name",
                table: "contact_addresses",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_addresses_contact_id_purpose",
                table: "contact_addresses",
                columns: new[] { "contact_id", "purpose" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contact_addresses_contact_id_purpose",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "is_in_liquidation",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "is_internal_company",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "is_sole_shareholder",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "logo_url",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "rea_number",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "rea_office",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "share_capital",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "access_notes",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "email",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "linked_warehouse_id",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "site_name",
                table: "contact_addresses");

            migrationBuilder.RenameColumn(
                name: "purpose",
                table: "contact_addresses",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "is_main_operational_site",
                table: "contact_addresses",
                newName: "is_default_shipping");

            migrationBuilder.CreateIndex(
                name: "i_x_contact_addresses_contact_id",
                table: "contact_addresses",
                column: "contact_id");
        }
    }
}
