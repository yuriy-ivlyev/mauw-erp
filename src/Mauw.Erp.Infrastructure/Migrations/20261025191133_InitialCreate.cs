using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <summary>
        /// Baseline squashed from the previous 42 migrations. It reproduces their resulting schema exactly,
        /// including the missing FKs on ddt_items/invoice_items/order_items/stock_movements.variant_id
        /// (the EF model declares them, but the squashed schema never had them): add them with a dedicated migration.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateSequence(
                name: "supplier_purchase_order_number_seq");

            migrationBuilder.CreateTable(
                name: "collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_collections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "colors",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    html_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_colors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    vat_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tax_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    sdi_code = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    pec_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "IT"),
                    fiscal_regime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_internal_company = table.Column<bool>(type: "boolean", nullable: false),
                    rea_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rea_office = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    share_capital = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_share_capital_fully_paid = table.Column<bool>(type: "boolean", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    bic_swift = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    invoice_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    is_sole_shareholder = table.Column<bool>(type: "boolean", nullable: true),
                    is_in_liquidation = table.Column<bool>(type: "boolean", nullable: false),
                    logo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    horizontal_logo_png = table.Column<byte[]>(type: "bytea", nullable: true),
                    compact_logo_png = table.Column<byte[]>(type: "bytea", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_contacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    iso_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_eu_member = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.iso_code);
                });

            migrationBuilder.CreateTable(
                name: "macro_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_macro_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "materials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_materials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "price_formulas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rounding_step = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    updated_by_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_singleton = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_price_formulas", x => x.id);
                    table.CheckConstraint("ck_price_formulas_is_singleton", "is_singleton");
                });

            migrationBuilder.CreateTable(
                name: "price_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_price_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_variant_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_product_variant_images", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sizes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_sizes", x => x.id);
                });

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
                    purpose = table.Column<int>(type: "integer", nullable: false),
                    site_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    street = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    zip_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "IT"),
                    is_main_operational_site = table.Column<bool>(type: "boolean", nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    access_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    formatted_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_place_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    linked_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    taxable_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_invoices", x => x.id);
                    table.ForeignKey(
                        name: "f_k_invoices_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    customer_discount_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    taxable_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    terminal_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    document_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    order_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_orders", x => x.id);
                    table.ForeignKey(
                        name: "f_k_orders_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "supplier_purchase_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_purchase_orders", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_purchase_orders_contacts_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    macro_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_categories", x => x.id);
                    table.ForeignKey(
                        name: "f_k_categories__macro_categories_macro_category_id",
                        column: x => x.macro_category_id,
                        principalTable: "macro_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "material_compositions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    percentage_value = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_material_compositions", x => x.id);
                    table.ForeignKey(
                        name: "f_k_material_compositions_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_formula_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_formula_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_price_formula_steps", x => x.id);
                    table.CheckConstraint("ck_price_formula_steps_percentage", "percentage >= 0 AND percentage <= 9999");
                    table.CheckConstraint("ck_price_formula_steps_position", "\"position\" >= 1");
                    table.ForeignKey(
                        name: "f_k_price_formula_steps_price_formulas_price_formula_id",
                        column: x => x.price_formula_id,
                        principalTable: "price_formulas",
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
                        name: "f_k_customer_profiles__price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_customer_profiles_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_sellable = table.Column<bool>(type: "boolean", nullable: false),
                    contact_address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_warehouses", x => x.id);
                    table.ForeignKey(
                        name: "f_k_warehouses_contact_addresses_contact_address_id",
                        column: x => x.contact_address_id,
                        principalTable: "contact_addresses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_commissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taxable_amount_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    commission_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    commission_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_agent_commissions", x => x.id);
                    table.ForeignKey(
                        name: "f_k_agent_commissions__contacts_agent_id",
                        column: x => x.agent_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_agent_commissions__invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_payments", x => x.id);
                    table.ForeignKey(
                        name: "f_k_payments_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    selling_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: true),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    base_purchase_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_id = table.Column<Guid>(type: "uuid", nullable: true),
                    has_variants = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_products", x => x.id);
                    table.ForeignKey(
                        name: "f_k_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_products_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_products_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ddt_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ddt_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ddt_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    taxable_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ddt_documents", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ddt_documents__warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_ddt_documents_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_delivery_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    document_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_delivery_notes", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_notes__supplier_purchase_orders_supplier_pur~",
                        column: x => x.supplier_purchase_order_id,
                        principalTable: "supplier_purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_notes__warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_notes_contacts_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    color_id = table.Column<string>(type: "character varying(3)", nullable: true),
                    size_id = table.Column<string>(type: "character varying(5)", nullable: true),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    purchase_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    selling_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: true),
                    weight_grams = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    variante_vetrina = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_product_variants", x => x.id);
                    table.ForeignKey(
                        name: "f_k_product_variants__sizes_size_id",
                        column: x => x.size_id,
                        principalTable: "sizes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_product_variants_colors_color_id",
                        column: x => x.color_id,
                        principalTable: "colors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_return_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_delivery_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_return_drafts", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_return_drafts_supplier_delivery_notes_supplier_del~",
                        column: x => x.supplier_delivery_note_id,
                        principalTable: "supplier_delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_return_drafts_supplier_purchase_orders_supplier_pu~",
                        column: x => x.supplier_purchase_order_id,
                        principalTable: "supplier_purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ddt_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ddt_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_net_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    line_net_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    line_vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    line_gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    nature_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    discount_percent = table.Column<decimal>(type: "numeric", nullable: false),
                    pricing_price_list_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_list_unit_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: true),
                    pricing_agreed_unit_net_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: true),
                    pricing_is_manual_override = table.Column<bool>(type: "boolean", nullable: true),
                    pricing_override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pricing_is_customer_return = table.Column<bool>(type: "boolean", nullable: true),
                    pricing_source_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_discounts = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ddt_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ddt_items_ddt_documents_ddt_document_id",
                        column: x => x.ddt_document_id,
                        principalTable: "ddt_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_net_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    line_net_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    line_vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    line_gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    nature_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    discount_percent = table.Column<decimal>(type: "numeric", nullable: false),
                    pricing_price_list_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_list_unit_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: true),
                    pricing_agreed_unit_net_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: true),
                    pricing_is_manual_override = table.Column<bool>(type: "boolean", nullable: true),
                    pricing_override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pricing_is_customer_return = table.Column<bool>(type: "boolean", nullable: true),
                    pricing_source_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_discounts = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_invoice_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_invoice_items_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_net_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    line_net_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    line_vat_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    line_gross_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    nature_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    discount_percent = table.Column<decimal>(type: "numeric", nullable: false),
                    max_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    pricing_price_list_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_list_unit_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: true),
                    pricing_agreed_unit_net_price = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: true),
                    pricing_is_manual_override = table.Column<bool>(type: "boolean", nullable: true),
                    pricing_override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pricing_is_customer_return = table.Column<bool>(type: "boolean", nullable: true),
                    pricing_source_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_discounts = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_order_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_list_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculation_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    value = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    minimum_quantity = table.Column<decimal>(type: "numeric(22,8)", precision: 22, scale: 8, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_price_list_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_list_rules_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_price_list_rules_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_price_list_rules_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_price_list_rules_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_on_hand = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    quantity_reserved = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(23,8)", precision: 23, scale: 8, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_stock_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_stock_items__warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_stock_items_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_article_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_item_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    article_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_article_mappings", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_article_mappings_contacts_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_article_mappings_product_variants_article_variant_~",
                        column: x => x.article_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_purchase_order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_ordered = table.Column<int>(type: "integer", nullable: false),
                    quantity_received = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_purchase_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_purchase_order_lines_product_variants_article_vari~",
                        column: x => x.article_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_purchase_order_lines_supplier_purchase_orders_supp~",
                        column: x => x.supplier_purchase_order_id,
                        principalTable: "supplier_purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_delivery_note_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_delivery_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    supplier_item_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    article_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    line_status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_delivery_note_lines", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_note_lines__supplier_purchase_order_lines_sup~",
                        column: x => x.supplier_purchase_order_line_id,
                        principalTable: "supplier_purchase_order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_note_lines_product_variants_article_varia~",
                        column: x => x.article_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_delivery_note_lines_supplier_delivery_notes_suppli~",
                        column: x => x.supplier_delivery_note_id,
                        principalTable: "supplier_delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    quantity_delta = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(23,8)", precision: 23, scale: 8, nullable: false),
                    reference_doc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<int>(type: "integer", nullable: true),
                    movement_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    supplier_delivery_note_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "f_k_stock_movements__supplier_delivery_note_lines_supplier_delivery~",
                        column: x => x.supplier_delivery_note_line_id,
                        principalTable: "supplier_delivery_note_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_stock_movements__warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "supplier_return_draft_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_return_draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_delivery_note_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_supplier_return_draft_lines", x => x.id);
                    table.ForeignKey(
                        name: "f_k_supplier_return_draft_lines_product_variants_article_varian~",
                        column: x => x.article_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_return_draft_lines_supplier_delivery_note_lines_su~",
                        column: x => x.supplier_delivery_note_line_id,
                        principalTable: "supplier_delivery_note_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_supplier_return_draft_lines_supplier_return_drafts_supplier~",
                        column: x => x.supplier_return_draft_id,
                        principalTable: "supplier_return_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "iso_code", "display_order", "is_eu_member", "name" },
                values: new object[,]
                {
                    { "AE", 97, false, "Emirati Arabi Uniti" },
                    { "AL", 54, false, "Albania" },
                    { "AR", 84, false, "Argentina" },
                    { "AT", 16, true, "Austria" },
                    { "AU", 95, false, "Australia" },
                    { "BA", 55, false, "Bosnia Erzegovina" },
                    { "BE", 14, true, "Belgio" },
                    { "BG", 27, true, "Bulgaria" },
                    { "BR", 83, false, "Brasile" },
                    { "CA", 81, false, "Canada" },
                    { "CH", 50, false, "Svizzera" },
                    { "CN", 90, false, "Cina" },
                    { "CY", 34, true, "Cipro" },
                    { "CZ", 24, true, "Repubblica Ceca" },
                    { "DE", 10, true, "Germania" },
                    { "DK", 21, true, "Danimarca" },
                    { "EE", 33, true, "Estonia" },
                    { "ES", 12, true, "Spagna" },
                    { "FI", 22, true, "Finlandia" },
                    { "FR", 11, true, "Francia" },
                    { "GB", 51, false, "Regno Unito" },
                    { "GR", 17, true, "Grecia" },
                    { "HR", 28, true, "Croazia" },
                    { "HU", 25, true, "Ungheria" },
                    { "IE", 23, true, "Irlanda" },
                    { "IN", 92, false, "India" },
                    { "IS", 53, false, "Islanda" },
                    { "IT", 1, true, "Italia" },
                    { "JP", 91, false, "Giappone" },
                    { "KR", 93, false, "Corea del Sud" },
                    { "LT", 31, true, "Lituania" },
                    { "LU", 36, true, "Lussemburgo" },
                    { "LV", 32, true, "Lettonia" },
                    { "ME", 57, false, "Montenegro" },
                    { "MK", 58, false, "Macedonia del Nord" },
                    { "MT", 35, true, "Malta" },
                    { "MX", 82, false, "Messico" },
                    { "NL", 13, true, "Paesi Bassi" },
                    { "NO", 52, false, "Norvegia" },
                    { "PL", 18, true, "Polonia" },
                    { "PT", 15, true, "Portogallo" },
                    { "RO", 26, true, "Romania" },
                    { "RS", 56, false, "Serbia" },
                    { "RU", 61, false, "Russia" },
                    { "SA", 98, false, "Arabia Saudita" },
                    { "SE", 20, true, "Svezia" },
                    { "SI", 30, true, "Slovenia" },
                    { "SK", 29, true, "Slovacchia" },
                    { "TR", 59, false, "Turchia" },
                    { "UA", 60, false, "Ucraina" },
                    { "US", 80, false, "Stati Uniti" },
                    { "ZA", 96, false, "Sudafrica" }
                });

            // Column defaults that existed in the squashed schema but are not part of the EF model.
            migrationBuilder.Sql("""
                ALTER TABLE categories ALTER COLUMN is_visible SET DEFAULT true;
                ALTER TABLE colors ALTER COLUMN is_visible SET DEFAULT true;
                ALTER TABLE macro_categories ALTER COLUMN is_visible SET DEFAULT true;
                ALTER TABLE materials ALTER COLUMN is_visible SET DEFAULT true;
                ALTER TABLE sizes ALTER COLUMN is_visible SET DEFAULT true;
                ALTER TABLE contacts ALTER COLUMN is_deleted SET DEFAULT false;
                ALTER TABLE contacts ALTER COLUMN is_in_liquidation SET DEFAULT false;
                ALTER TABLE contacts ALTER COLUMN is_internal_company SET DEFAULT false;
                ALTER TABLE contacts ALTER COLUMN is_share_capital_fully_paid SET DEFAULT false;
                ALTER TABLE products ALTER COLUMN base_purchase_cost SET DEFAULT 0.0;
                ALTER TABLE ddt_items ALTER COLUMN discount_percent SET DEFAULT 0.0;
                ALTER TABLE invoice_items ALTER COLUMN discount_percent SET DEFAULT 0.0;
                ALTER TABLE order_items ALTER COLUMN discount_percent SET DEFAULT 0.0;
                """);

            // Placeholder catalog data carried over from the squashed migrations (ids generated at apply time, as before).
            var now = DateTime.UtcNow;
            var clothingMacroId = Guid.NewGuid();
            var accessoriesMacroId = Guid.NewGuid();

            migrationBuilder.InsertData(
                table: "macro_categories",
                columns: new[] { "id", "name", "created_at", "updated_at" },
                values: new object[,]
                {
                    { clothingMacroId, "Clothing", now, now },
                    { accessoriesMacroId, "Accessories", now, now }
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "name", "macro_category_id", "created_at", "updated_at" },
                values: new object[,]
                {
                    { Guid.NewGuid(), "T-Shirts", clothingMacroId, now, now },
                    { Guid.NewGuid(), "Pants", clothingMacroId, now, now },
                    { Guid.NewGuid(), "Shoes", clothingMacroId, now, now },
                    { Guid.NewGuid(), "Bags", accessoriesMacroId, now, now },
                    { Guid.NewGuid(), "Hats", accessoriesMacroId, now, now }
                });

            migrationBuilder.InsertData(
                table: "collections",
                columns: new[] { "id", "name", "description", "created_at", "updated_at" },
                values: new object[,]
                {
                    { Guid.NewGuid(), "Summer 2024", "Summer collection 2024", now, now },
                    { Guid.NewGuid(), "Winter 2024", "Winter collection 2024", now, now },
                    { Guid.NewGuid(), "Spring 2025", "Spring collection 2025", now, now }
                });

            var cottonMaterialId = Guid.NewGuid();
            var polyesterMaterialId = Guid.NewGuid();
            var blendMaterialId = Guid.NewGuid();

            migrationBuilder.InsertData(
                table: "materials",
                columns: new[] { "id", "name", "created_at", "updated_at" },
                values: new object[,]
                {
                    { cottonMaterialId, "100% Cotton", now, now },
                    { polyesterMaterialId, "Polyester", now, now },
                    { blendMaterialId, "Cotton Blend", now, now }
                });

            migrationBuilder.InsertData(
                table: "material_compositions",
                columns: new[] { "id", "material_id", "component_name", "percentage_value", "created_at", "updated_at" },
                values: new object[,]
                {
                    { Guid.NewGuid(), cottonMaterialId, "Cotton", 100.00m, now, now },
                    { Guid.NewGuid(), polyesterMaterialId, "Polyester", 100.00m, now, now },
                    { Guid.NewGuid(), blendMaterialId, "Cotton", 70.00m, now, now },
                    { Guid.NewGuid(), blendMaterialId, "Polyester", 30.00m, now, now }
                });

            migrationBuilder.CreateIndex(
                name: "i_x_agent_commissions_invoice_id",
                table: "agent_commissions",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_agent_id_status",
                table: "agent_commissions",
                columns: new[] { "agent_id", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_categories_macro_category_id",
                table: "categories",
                column: "macro_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_contact_addresses_contact_id_purpose",
                table: "contact_addresses",
                columns: new[] { "contact_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_addresses_latitude_longitude",
                table: "contact_addresses",
                columns: new[] { "latitude", "longitude" },
                filter: "latitude IS NOT NULL AND longitude IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_contact_persons_contact_id",
                table: "contact_persons",
                column: "contact_id");

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
                name: "IX_countries_display_order",
                table: "countries",
                column: "display_order");

            migrationBuilder.CreateIndex(
                name: "i_x_credit_notes_invoice_id",
                table: "credit_notes",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_credit_note_number",
                table: "credit_notes",
                column: "credit_note_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_customer_profiles_price_list_id",
                table: "customer_profiles",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ddt_documents_warehouse_id",
                table: "ddt_documents",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_ddt_documents_contact_id_ddt_date",
                table: "ddt_documents",
                columns: new[] { "contact_id", "ddt_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ddt_documents_ddt_number",
                table: "ddt_documents",
                column: "ddt_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ddt_documents_order_id",
                table: "ddt_documents",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ddt_items_ddt_document_id",
                table: "ddt_items",
                column: "ddt_document_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ddt_items_variant_id",
                table: "ddt_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "i_x_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "i_x_invoice_items_variant_id",
                table: "invoice_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_contact_id_invoice_date",
                table: "invoices",
                columns: new[] { "contact_id", "invoice_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_invoice_number",
                table: "invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_order_id",
                table: "invoices",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "i_x_material_compositions_material_id",
                table: "material_compositions",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "i_x_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "i_x_order_items_variant_id",
                table: "order_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_contact_id_order_date",
                table: "orders",
                columns: new[] { "contact_id", "order_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_number",
                table: "orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_payments_invoice_id",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ux_price_formula_steps_price_formula_id_position",
                table: "price_formula_steps",
                columns: new[] { "price_formula_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_price_formulas_is_singleton",
                table: "price_formulas",
                column: "is_singleton",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_category_id",
                table: "price_list_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_price_list_id_category_id",
                table: "price_list_rules",
                columns: new[] { "price_list_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_price_list_id_product_id",
                table: "price_list_rules",
                columns: new[] { "price_list_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_price_list_id_variant_id",
                table: "price_list_rules",
                columns: new[] { "price_list_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_product_id",
                table: "price_list_rules",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_list_rules_variant_id",
                table: "price_list_rules",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_images_file_path",
                table: "product_variant_images",
                column: "file_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_images_target_id_sort_order",
                table: "product_variant_images",
                columns: new[] { "target_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "i_x_product_variants_barcode_trgm",
                table: "product_variants",
                column: "barcode")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "i_x_product_variants_color_id",
                table: "product_variants",
                column: "color_id");

            migrationBuilder.CreateIndex(
                name: "i_x_product_variants_product_id",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "i_x_product_variants_size_id",
                table: "product_variants",
                column: "size_id");

            migrationBuilder.CreateIndex(
                name: "i_x_product_variants_supplier_code_trgm",
                table: "product_variants",
                column: "supplier_code")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_barcode",
                table: "product_variants",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_supplier_id_supplier_code",
                table: "product_variants",
                columns: new[] { "supplier_id", "supplier_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "i_x_products_collection_id",
                table: "products",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "i_x_products_material_id",
                table: "products",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "i_x_products_name_trgm",
                table: "products",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "i_x_products_supplier_code_trgm",
                table: "products",
                column: "supplier_code")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_products_barcode",
                table: "products",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_supplier_id_supplier_code",
                table: "products",
                columns: new[] { "supplier_id", "supplier_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_stock_items_product_variant_id",
                table: "stock_items",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "i_x_stock_items_warehouse_id",
                table: "stock_items",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_warehouse_id_product_variant_id",
                table: "stock_items",
                columns: new[] { "warehouse_id", "product_variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_stock_movements_cost_warehouse_variant_date",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "variant_id", "movement_date_utc", "id" },
                descending: new[] { false, false, true, true },
                filter: "warehouse_id IS NOT NULL AND quantity_delta IS NOT NULL AND quantity_delta > 0 AND unit_cost > 0")
                .Annotation("Npgsql:IndexInclude", new[] { "quantity_delta", "quantity", "type", "unit_cost" });

            migrationBuilder.CreateIndex(
                name: "i_x_stock_movements_reference_id",
                table: "stock_movements",
                column: "reference_id");

            migrationBuilder.CreateIndex(
                name: "i_x_stock_movements_supplier_delivery_note_line_id",
                table: "stock_movements",
                column: "supplier_delivery_note_line_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_stock_movements_warehouse_id_movement_date_utc",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "movement_date_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_stock_movements_warehouse_variant_movement_date_utc",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "variant_id", "movement_date_utc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_movement_date_utc",
                table: "stock_movements",
                column: "movement_date_utc");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_variant_id_created_at_id",
                table: "stock_movements",
                columns: new[] { "variant_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_warehouse_id_variant_id",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_article_mappings_article_variant_id",
                table: "supplier_article_mappings",
                column: "article_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_article_mappings_supplier_id_supplier_item_code",
                table: "supplier_article_mappings",
                columns: new[] { "supplier_id", "supplier_item_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_note_lines_article_variant_id",
                table: "supplier_delivery_note_lines",
                column: "article_variant_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_note_lines_supplier_purchase_order_line_id",
                table: "supplier_delivery_note_lines",
                column: "supplier_purchase_order_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_delivery_note_lines_supplier_delivery_note_id_posi~",
                table: "supplier_delivery_note_lines",
                columns: new[] { "supplier_delivery_note_id", "position" });

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_notes_supplier_id",
                table: "supplier_delivery_notes",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_notes_supplier_purchase_order_id",
                table: "supplier_delivery_notes",
                column: "supplier_purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_delivery_notes_warehouse_id",
                table: "supplier_delivery_notes",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_delivery_notes_uploaded_at_id",
                table: "supplier_delivery_notes",
                columns: new[] { "uploaded_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_purchase_order_lines_article_variant_id",
                table: "supplier_purchase_order_lines",
                column: "article_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_purchase_order_lines_supplier_purchase_order_id_ar~",
                table: "supplier_purchase_order_lines",
                columns: new[] { "supplier_purchase_order_id", "article_variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_purchase_orders_supplier_id",
                table: "supplier_purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_purchase_orders_order_number",
                table: "supplier_purchase_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_purchase_orders_status_supplier_id_order_date",
                table: "supplier_purchase_orders",
                columns: new[] { "status", "supplier_id", "order_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_return_draft_lines_article_variant_id",
                table: "supplier_return_draft_lines",
                column: "article_variant_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_return_draft_lines_supplier_delivery_note_line_id",
                table: "supplier_return_draft_lines",
                column: "supplier_delivery_note_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_draft_lines_supplier_return_draft_id_suppli~",
                table: "supplier_return_draft_lines",
                columns: new[] { "supplier_return_draft_id", "supplier_delivery_note_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_return_drafts_supplier_delivery_note_id",
                table: "supplier_return_drafts",
                column: "supplier_delivery_note_id");

            migrationBuilder.CreateIndex(
                name: "i_x_supplier_return_drafts_supplier_purchase_order_id",
                table: "supplier_return_drafts",
                column: "supplier_purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "i_x_warehouses_contact_address_id",
                table: "warehouses",
                column: "contact_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_code",
                table: "warehouses",
                column: "code",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_commissions");

            migrationBuilder.DropTable(
                name: "agent_profiles");

            migrationBuilder.DropTable(
                name: "contact_persons");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropTable(
                name: "customer_profiles");

            migrationBuilder.DropTable(
                name: "ddt_items");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "material_compositions");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "price_formula_steps");

            migrationBuilder.DropTable(
                name: "price_list_rules");

            migrationBuilder.DropTable(
                name: "product_variant_images");

            migrationBuilder.DropTable(
                name: "stock_items");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "supplier_article_mappings");

            migrationBuilder.DropTable(
                name: "supplier_profiles");

            migrationBuilder.DropTable(
                name: "supplier_return_draft_lines");

            migrationBuilder.DropTable(
                name: "ddt_documents");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "price_formulas");

            migrationBuilder.DropTable(
                name: "price_lists");

            migrationBuilder.DropTable(
                name: "supplier_delivery_note_lines");

            migrationBuilder.DropTable(
                name: "supplier_return_drafts");

            migrationBuilder.DropTable(
                name: "supplier_purchase_order_lines");

            migrationBuilder.DropTable(
                name: "supplier_delivery_notes");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "supplier_purchase_orders");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropTable(
                name: "sizes");

            migrationBuilder.DropTable(
                name: "colors");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "contact_addresses");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "collections");

            migrationBuilder.DropTable(
                name: "materials");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropTable(
                name: "macro_categories");

            migrationBuilder.DropSequence(
                name: "supplier_purchase_order_number_seq");
        }
    }
}
