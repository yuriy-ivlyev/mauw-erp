using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditNoteSdiStorageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sdi_file_name",
                table: "credit_notes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sdi_storage_key",
                table: "credit_notes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sdi_transmission_id",
                table: "credit_notes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sdi_transmitted_at_utc",
                table: "credit_notes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sdi_file_name",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "sdi_storage_key",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "sdi_transmission_id",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "sdi_transmitted_at_utc",
                table: "credit_notes");
        }
    }
}
