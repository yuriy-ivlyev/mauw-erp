using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorContactPartyRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_contacts_contacts_agent_id",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "i_x_contacts_agent_id",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_vat_number",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "agent_id",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "default_commission_rate",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "default_discount_rate",
                table: "contacts");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "contacts",
                newName: "status");

            migrationBuilder.AlterColumn<string>(
                name: "vat_number",
                table: "contacts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "sdi_code",
                table: "contacts",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "contacts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "IT");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "contacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "contacts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fiscal_regime",
                table: "contacts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "pec_email",
                table: "contacts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                table: "contacts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agent_profiles",
                columns: table => new
                {
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_commission_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    operating_area = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_profiles", x => x.contact_id);
                    table.ForeignKey(
                        name: "f_k_agent_profiles__contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    street = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    zip_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    province = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "IT"),
                    is_default_shipping = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_contact_addresses", x => x.id);
                    table.ForeignKey(
                        name: "f_k_contact_addresses_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_contact_persons", x => x.id);
                    table.ForeignKey(
                        name: "f_k_contact_persons_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                columns: table => new
                {
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_discount_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    block_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_profiles", x => x.contact_id);
                    table.ForeignKey(
                        name: "f_k_customer_profiles_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_profiles",
                columns: table => new
                {
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    bic_swift = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_profiles", x => x.contact_id);
                    table.ForeignKey(
                        name: "f_k_supplier_profiles_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_business_name",
                table: "contacts",
                column: "business_name");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_status",
                table: "contacts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_tax_code",
                table: "contacts",
                column: "tax_code",
                filter: "tax_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_vat_number",
                table: "contacts",
                column: "vat_number",
                unique: true,
                filter: "vat_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_contact_addresses_contact_id",
                table: "contact_addresses",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "i_x_contact_persons_contact_id",
                table: "contact_persons",
                column: "contact_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_profiles");

            migrationBuilder.DropTable(
                name: "contact_addresses");

            migrationBuilder.DropTable(
                name: "contact_persons");

            migrationBuilder.DropTable(
                name: "customer_profiles");

            migrationBuilder.DropTable(
                name: "supplier_profiles");

            migrationBuilder.DropIndex(
                name: "IX_contacts_business_name",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_status",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_tax_code",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_vat_number",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "fiscal_regime",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "pec_email",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "tax_code",
                table: "contacts");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "contacts",
                newName: "type");

            migrationBuilder.AlterColumn<string>(
                name: "vat_number",
                table: "contacts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "sdi_code",
                table: "contacts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "agent_id",
                table: "contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "default_commission_rate",
                table: "contacts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "default_discount_rate",
                table: "contacts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "i_x_contacts_agent_id",
                table: "contacts",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_vat_number",
                table: "contacts",
                column: "vat_number",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "f_k_contacts_contacts_agent_id",
                table: "contacts",
                column: "agent_id",
                principalTable: "contacts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
