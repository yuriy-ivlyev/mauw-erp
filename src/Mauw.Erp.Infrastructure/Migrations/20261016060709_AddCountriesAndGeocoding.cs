using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCountriesAndGeocoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "province",
                table: "contact_addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_place_id",
                table: "contact_addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "formatted_address",
                table: "contact_addresses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "contact_addresses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "contact_addresses",
                type: "double precision",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_contact_addresses_latitude_longitude",
                table: "contact_addresses",
                columns: new[] { "latitude", "longitude" },
                filter: "latitude IS NOT NULL AND longitude IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_countries_display_order",
                table: "countries",
                column: "display_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropIndex(
                name: "IX_contact_addresses_latitude_longitude",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "external_place_id",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "formatted_address",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "contact_addresses");

            migrationBuilder.AlterColumn<string>(
                name: "province",
                table: "contact_addresses",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
