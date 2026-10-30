using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderFulfillmentAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "approval_reason",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "box_count",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_name",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ddt_number",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "fulfillment_type",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "fulfillment_warehouse_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hold_reason",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status_before_hold",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tracking_code",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "warehouse_operator_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_kg",
                table: "orders",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE orders SET fulfillment_type = 1 WHERE order_type = 1");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    available_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_orders_fulfillment_warehouse_id",
                table: "orders",
                column: "fulfillment_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                table: "outbox_messages",
                columns: new[] { "processed_on_utc", "available_on_utc", "occurred_on_utc" });

            migrationBuilder.AddForeignKey(
                name: "f_k_orders__warehouses_fulfillment_warehouse_id",
                table: "orders",
                column: "fulfillment_warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_orders__warehouses_fulfillment_warehouse_id",
                table: "orders");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "i_x_orders_fulfillment_warehouse_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "approval_reason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "box_count",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "carrier_name",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ddt_number",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "fulfillment_type",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "fulfillment_warehouse_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "hold_reason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "status_before_hold",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tracking_code",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "warehouse_operator_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "weight_kg",
                table: "orders");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "orders",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }
    }
}
