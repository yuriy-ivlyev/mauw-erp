using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCassaSessionToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La tabella non e' creata da nessuna migrazione precedente: su un database nuovo non esiste.
            migrationBuilder.Sql("DROP TABLE IF EXISTS cassa_sessions;");

            migrationBuilder.AddColumn<string>(
                name: "document_reference",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terminal_id",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_quantity",
                table: "order_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_reference",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "terminal_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "max_quantity",
                table: "order_items");

            migrationBuilder.CreateTable(
                name: "cassa_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_json = table.Column<string>(type: "text", nullable: false),
                    contact_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    session_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    terminal_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_cassa_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cassa_sessions_session_code",
                table: "cassa_sessions",
                column: "session_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cassa_sessions_status",
                table: "cassa_sessions",
                column: "status");
        }
    }
}
